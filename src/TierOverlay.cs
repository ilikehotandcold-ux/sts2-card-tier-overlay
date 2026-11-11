using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Saves;

namespace StsTierOverlay;

/// <summary>
/// Shows the Mobalytics tier (S/A/B/C/D) as a badge on cards offered in card rewards,
/// "choose a card" screens and the merchant.
///
/// Built to survive game updates:
///  * No hooks into specific screen methods. Every NCard that enters the scene tree is checked, and it gets a
///    badge when one of its ancestors' type names contains a "screen pattern" (e.g. "CardReward", "Merchant").
///  * Screen patterns and tiers live in a JSON file that is refreshed from GitHub at startup, so a renamed
///    screen or a new tier list can be fixed for everyone by editing data/tiers.json in the repo.
///  * The only Harmony patches (keeping the normal save profile) are applied one by one and may fail safely.
///  * A health report (status.txt) is written next to the DLL.
/// </summary>
[ModInitializer(nameof(Init))]
public static class TierOverlay
{
    public const string Version = "1.2.0";
    private const string RemoteUrl =
        "https://raw.githubusercontent.com/ilikehotandcold-ux/sts2-card-tier-overlay/main/data/tiers.json";
    private const string MobalyticsUrl = "https://mobalytics.gg/slay-the-spire-2/tier-lists/cards";
    private const string LocalFile = "tiers.txt";        // .txt: the game reads every *.json in a mod folder as a manifest
    private const string NoUpdateFile = "no_update.txt";
    private const string StatusFile = "status.txt";
    private const string HelperPage = "update.html";
    private const string BookmarkOkFile = "bookmark_ok.txt";
    private const string DownloadPattern = "sts2-tiers*.json";
    private const string BadgeMeta = "moba_tier_badge";

    internal sealed class TierData
    {
        public Dictionary<string, string> Tiers = new();          // normalized card name -> "S".."D"
        public string[] ScreenPatterns = { "CardReward", "ChooseACard", "Merchant" };
        public List<string> RawNames = new();                    // for the health report
        public string Source = "";
        public string Updated = "";                              // "yyyy-MM-dd" from "_updated"; newest wins
    }

    private static volatile TierData _data = new();
    private static readonly object SaveLock = new();
    private static readonly List<string> PatchReport = new();
    private static string? _modDir;

    private static readonly Dictionary<string, Color> TierColors = new()
    {
        ["S"] = new Color("ff4d4d"),
        ["A"] = new Color("ff9a2e"),
        ["B"] = new Color("ffd93b"),
        ["C"] = new Color("6fd65a"),
        ["D"] = new Color("59a8ff"),
    };

    public static void Init()
    {
        _modDir = Path.GetDirectoryName(typeof(TierOverlay).Assembly.Location);

        try { _data = LoadLocal(); }
        catch (Exception e) { GD.PrintErr($"[StsTierOverlay] Could not load tiers: {e}"); }
        GD.Print($"[StsTierOverlay] v{Version}: {_data.Tiers.Count} card tiers from {_data.Source}.");

        // Picks up a file the bookmarklet downloaded while the game was closed.
        try { TierUpdater.ImportFromDownloads(); }
        catch (Exception e) { GD.PrintErr($"[StsTierOverlay] Downloads check failed: {e.Message}"); }

        ApplyPatches();

        try
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            tree.NodeAdded += OnNodeAdded;
            PatchReport.Add("OK   card watcher (SceneTree.NodeAdded)");
        }
        catch (Exception e)
        {
            PatchReport.Add($"FAIL card watcher: {e.Message}");
            GD.PrintErr($"[StsTierOverlay] Card watcher failed: {e}");
        }

        WriteStatus();
        _ = Task.Run(UpdateFromGitHub);
    }

    // ---------------------------------------------------------------- data

    private static TierData LoadLocal()
    {
        string? path = _modDir == null ? null : Path.Combine(_modDir, LocalFile);
        if (path != null && File.Exists(path))
        {
            try { return Parse(File.ReadAllText(path), LocalFile); }
            catch (Exception e) { GD.PrintErr($"[StsTierOverlay] {LocalFile} is invalid, using built-in data: {e.Message}"); }
        }
        using Stream s = typeof(TierOverlay).Assembly.GetManifestResourceStream("tiers.txt")!;
        using var r = new StreamReader(s);
        return Parse(r.ReadToEnd(), "built-in");
    }

    /// <summary>{ "_screens": [...], "Ironclad": { "S": ["offering", ...], ... }, ... }</summary>
    private static TierData Parse(string json, string source)
    {
        var data = new TierData { Source = source };
        using JsonDocument doc = JsonDocument.Parse(json);
        foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name == "_updated" && prop.Value.ValueKind == JsonValueKind.String)
            {
                data.Updated = prop.Value.GetString() ?? "";
                continue;
            }
            if (prop.Name == "_screens" && prop.Value.ValueKind == JsonValueKind.Array)
            {
                string[] patterns = prop.Value.EnumerateArray().Select(e => e.GetString() ?? "").Where(p => p.Length > 0).ToArray();
                if (patterns.Length > 0) data.ScreenPatterns = patterns;
                continue;
            }
            if (prop.Name.StartsWith("_") || prop.Value.ValueKind != JsonValueKind.Object) continue;

            foreach (JsonProperty tier in prop.Value.EnumerateObject())
            {
                if (tier.Value.ValueKind != JsonValueKind.Array) continue;
                foreach (JsonElement card in tier.Value.EnumerateArray())
                {
                    string slug = card.GetString() ?? "";
                    if (slug.EndsWith("-plus")) slug = slug[..^5];
                    if (data.Tiers.TryAdd(Normalize(slug), tier.Name.ToUpperInvariant())) data.RawNames.Add(slug);
                }
            }
        }
        if (data.Tiers.Count == 0) throw new InvalidDataException("no cards found");
        return data;
    }

    private static async Task UpdateFromGitHub()
    {
        if (_modDir == null || File.Exists(Path.Combine(_modDir, NoUpdateFile))) return;
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("StsTierOverlay/" + Version);
            string json = await http.GetStringAsync(RemoteUrl).ConfigureAwait(false);
            TierData fresh = Parse(json, "GitHub");
            if (TryApply(json, fresh))
                GD.Print($"[StsTierOverlay] Tiers updated from GitHub: {fresh.Tiers.Count} cards ({fresh.Updated}).");
            else
                GD.Print($"[StsTierOverlay] GitHub tiers ({fresh.Updated}) are not newer than local ({_data.Updated}); kept local.");
        }
        catch (Exception e)
        {
            GD.Print($"[StsTierOverlay] Online update skipped ({e.GetType().Name}); using {_data.Source}.");
        }
    }

    /// <summary>
    /// Saves and activates new tier data unless what we already have is newer (by "_updated").
    /// <paramref name="force"/> skips the date check (manual update from Mobalytics).
    /// </summary>
    internal static bool TryApply(string json, TierData fresh, bool force = false)
    {
        if (_modDir == null || fresh.Tiers.Count < 50) return false; // refuse obviously broken data
        lock (SaveLock)
        {
            if (!force && string.CompareOrdinal(fresh.Updated, _data.Updated) < 0) return false;
            string path = Path.Combine(_modDir, LocalFile);
            string? old = File.Exists(path) ? File.ReadAllText(path) : null;
            if (old != json) File.WriteAllText(path, json, new UTF8Encoding(false));
            _data = fresh;
        }
        WriteStatus();
        return true;
    }

    internal static TierData ParseFile(string json, string source) => Parse(json, source);
    internal static string? ModDir => _modDir;
    internal static int CardCount => _data.Tiers.Count;
    internal static string DataDate => _data.Updated;
    internal static string MobalyticsPage => MobalyticsUrl;
    internal static string HelperPageFile => HelperPage;
    internal static string BookmarkOk => BookmarkOkFile;
    internal static string DownloadFilePattern => DownloadPattern;

    private static string Normalize(string s) =>
        new string(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string? TierFor(CardModel? model)
    {
        if (model == null) return null;
        return _data.Tiers.TryGetValue(Normalize(model.GetType().Name), out string? t) ? t : null;
    }

    // ---------------------------------------------------------------- watcher

    private static void OnNodeAdded(Node node)
    {
        if (node is NCard card)
        {
            // Parent chain and Model are complete by the next idle step.
            Callable.From(() => Evaluate(card)).CallDeferred();
        }
        else if (node is Control menu && node.GetType().Name == "NMainMenu")
        {
            Callable.From(() => TierUpdater.AddButton(menu)).CallDeferred();
        }
    }

    private static void Evaluate(NCard card)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(card) || !card.IsInsideTree()) return;
            if (FindBadge(card) != null || !IsInOfferScreen(card)) return;
            Decorate(card);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[StsTierOverlay] {e.Message}");
        }
    }

    private static bool IsInOfferScreen(Node card)
    {
        string[] patterns = _data.ScreenPatterns;
        for (Node? n = card.GetParent(); n != null; n = n.GetParent())
        {
            string typeName = n.GetType().Name;
            foreach (string p in patterns)
            {
                if (typeName.Contains(p, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- badge

    private static Control? FindBadge(NCard card) =>
        card.GetChildren().OfType<Control>().FirstOrDefault(c => c.HasMeta(BadgeMeta) && !c.IsQueuedForDeletion());

    private static void Decorate(NCard card)
    {
        string? tier = TierFor(card.Model);
        if (tier == null) return;

        Color color = TierColors.TryGetValue(tier, out Color c) ? c : Colors.White;
        const float size = 64f;

        var style = new StyleBoxFlat
        {
            BgColor = color,
            BorderColor = new Color(0.08f, 0.06f, 0.05f),
            ShadowColor = new Color(0, 0, 0, 0.6f),
            ShadowSize = 6,
        };
        style.SetBorderWidthAll(4);
        style.SetCornerRadiusAll((int)(size / 2));

        var panel = new Panel
        {
            Name = "MobaTierBadge",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(size, size),
            Position = CardTopLeft(card) - new Vector2(14f, 14f),
        };
        panel.SetMeta(BadgeMeta, true);
        panel.AddThemeStyleboxOverride("panel", style);

        var label = new Label
        {
            Text = tier,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(size, size),
        };
        label.AddThemeFontSizeOverride("font_size", 42);
        label.AddThemeColorOverride("font_color", new Color(0.1f, 0.07f, 0.05f));
        label.AddThemeColorOverride("font_outline_color", new Color(1f, 1f, 1f, 0.85f));
        label.AddThemeConstantOverride("outline_size", 4);
        Font? gameFont = FindGameFont(card);
        if (gameFont != null) label.AddThemeFontOverride("font", gameFont);
        panel.AddChild(label);

        card.AddChild(panel);

        // Cards are pooled and reused (e.g. in the combat hand): drop the badge as soon as the card leaves this screen.
        card.Connect(Node.SignalName.TreeExiting, Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(panel)) panel.QueueFree();
        }), (uint)GodotObject.ConnectFlags.OneShot);
    }

    /// <summary>Top-left corner of the card frame, in the NCard's local coordinates.</summary>
    private static Vector2 CardTopLeft(NCard card)
    {
        Vector2 fallback = -NCard.defaultSize / 2f; // NCard is laid out around its origin
        try
        {
            if (card.GetNodeOrNull<Control>("%Frame") is Control frame && frame.IsInsideTree())
            {
                Vector2 p = card.GetGlobalTransform().AffineInverse() * frame.GetGlobalTransform().Origin;
                if (Mathf.Abs(p.X) < 400f && Mathf.Abs(p.Y) < 400f) return p;
            }
        }
        catch { /* use fallback */ }
        return fallback;
    }

    private static Font? FindGameFont(NCard card)
    {
        try
        {
            if (card.FindChild("TitleLabel", true, false) is Label l) return l.GetThemeFont("font");
        }
        catch { /* cosmetic only */ }
        return null;
    }

    // ---------------------------------------------------------------- patches & report

    private static void ApplyPatches()
    {
        var harmony = new Harmony("StsTierOverlay");
        foreach (Type type in typeof(TierOverlay).Assembly.GetTypes())
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
            try
            {
                harmony.CreateClassProcessor(type).Patch();
                PatchReport.Add($"OK   {type.Name}");
            }
            catch (Exception e)
            {
                PatchReport.Add($"FAIL {type.Name}: {e.Message}");
                GD.PrintErr($"[StsTierOverlay] Patch {type.Name} skipped (game version changed?): {e.Message}");
            }
        }
    }

    private static void WriteStatus()
    {
        if (_modDir == null) return;
        try
        {
            TierData data = _data;
            HashSet<string> gameCards = typeof(CardModel).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(CardModel).IsAssignableFrom(t))
                .Select(t => Normalize(t.Name))
                .ToHashSet();
            List<string> unknown = data.RawNames.Where(n => !gameCards.Contains(Normalize(n))).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Card Tier Overlay v{Version} - {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Data: {data.Source} (updated {data.Updated}), {data.Tiers.Count} cards with a tier");
            sb.AppendLine($"Screens: {string.Join(", ", data.ScreenPatterns)}");
            sb.AppendLine($"Cards found in the game: {gameCards.Count}");
            sb.AppendLine();
            sb.AppendLine("Components:");
            foreach (string line in PatchReport) sb.AppendLine("  " + line);
            sb.AppendLine();
            sb.AppendLine($"Tier list names that do not exist in this game version ({unknown.Count}):");
            sb.AppendLine("  " + (unknown.Count == 0 ? "(none)" : string.Join(", ", unknown)));
            File.WriteAllText(Path.Combine(_modDir, StatusFile), sb.ToString(), new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            GD.PrintErr($"[StsTierOverlay] Could not write {StatusFile}: {e.Message}");
        }
    }
}

// The game switches to a separate, empty "modded" save profile whenever any mod is loaded.
// This mod is purely visual, so keep using the normal profile (progress, timeline, unlocks) as long as
// every loaded mod declares "affects_gameplay": false. Other gameplay mods keep the vanilla behaviour.
internal static class NormalProfile
{
    internal static bool OnlyVisualModsLoaded() =>
        ModManager.Mods
            .Where(m => m.state == ModLoadState.Loaded)
            .All(m => m.manifest != null && !m.manifest.affectsGameplay);
}

[HarmonyPatch(typeof(UserDataPathProvider), nameof(UserDataPathProvider.IsRunningModded), MethodType.Setter)]
internal static class KeepNormalProfileSetterPatch
{
    private static void Prefix(ref bool value)
    {
        try { if (value && NormalProfile.OnlyVisualModsLoaded()) value = false; }
        catch { /* fall back to the game's choice */ }
    }
}

// Backup in case the tiny setter above got inlined by the JIT: enforce right before the profile path is chosen.
[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.InitProfileId))]
internal static class KeepNormalProfileInitPatch
{
    private static void Prefix()
    {
        try
        {
            if (UserDataPathProvider.IsRunningModded && NormalProfile.OnlyVisualModsLoaded())
            {
                UserDataPathProvider.IsRunningModded = false;
            }
            if (!UserDataPathProvider.IsRunningModded)
                GD.Print("[StsTierOverlay] Only visual mods loaded: using the normal save profile.");
        }
        catch { /* fall back to the game's choice */ }
    }
}
