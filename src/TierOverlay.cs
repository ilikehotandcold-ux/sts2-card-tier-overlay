using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Rewards;

namespace StsTierOverlay;

/// <summary>
/// Shows the Mobalytics tier (S/A/B/C/D) as a small badge on cards and relics you are offered:
/// card rewards, "choose a card", treasure chests, "choose a relic", relic rewards, Ancient (Neow) options and the
/// merchant; plus your deck, card inspect screens and relic tooltips.
///
/// Built to survive game updates:
///  * No hooks into specific screen methods. Every card/relic node that enters the scene tree is checked, and it
///    gets a badge when one of its ancestors' type names contains a "screen pattern" (e.g. "CardReward").
///  * Screen patterns and tiers live in data/tiers.json, downloaded from GitHub at every start, so a renamed
///    screen or a new tier list is fixed for everyone by editing that file in the repo.
///  * The only Harmony patches (keeping the normal save profile) are applied one by one and may fail safely.
///  * A health report (status.txt) is written next to the DLL.
/// </summary>
[ModInitializer(nameof(Init))]
public static class TierOverlay
{
    public const string Version = "1.5.0";
    private const string RemoteUrl =
        "https://raw.githubusercontent.com/ilikehotandcold-ux/sts2-card-tier-overlay/main/data/tiers.json";
    private const string LocalFile = "tiers.txt";        // .txt: the game reads every *.json in a mod folder as a manifest
    private const string NoUpdateFile = "no_update.txt";
    private const string StatusFile = "status.txt";
    private const string WatchMeta = "moba_tier_watch";

    private static volatile TierData _data = new();
    private static readonly object SaveLock = new();
    private static readonly List<string> Report = new();
    private static string? _modDir;

    public static void Init()
    {
        _modDir = Path.GetDirectoryName(typeof(TierOverlay).Assembly.Location);

        try { _data = LoadLocal(); }
        catch (Exception e) { GD.PrintErr($"[StsTierOverlay] Could not load tiers: {e}"); }
        GD.Print($"[StsTierOverlay] v{Version}: {_data.Cards.Count} cards, {_data.Relics.Count} relics from {_data.Source}.");

        ApplyPatches();

        try
        {
            ((SceneTree)Engine.GetMainLoop()).NodeAdded += OnNodeAdded;
            Report.Add("OK   watcher (SceneTree.NodeAdded)");
        }
        catch (Exception e)
        {
            Report.Add($"FAIL watcher: {e.Message}");
            GD.PrintErr($"[StsTierOverlay] Watcher failed: {e}");
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
            try { return TierData.Parse(File.ReadAllText(path), LocalFile); }
            catch (Exception e) { GD.PrintErr($"[StsTierOverlay] {LocalFile} is invalid, using built-in data: {e.Message}"); }
        }
        using Stream s = typeof(TierOverlay).Assembly.GetManifestResourceStream("tiers.txt")!;
        using var r = new StreamReader(s);
        return TierData.Parse(r.ReadToEnd(), "built-in");
    }

    private static async Task UpdateFromGitHub()
    {
        if (_modDir == null || File.Exists(Path.Combine(_modDir, NoUpdateFile))) return;
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("StsTierOverlay/" + Version);
            string json = await http.GetStringAsync(RemoteUrl).ConfigureAwait(false);
            TierData fresh = TierData.Parse(json, "GitHub");
            if (fresh.Cards.Count + fresh.Relics.Count < 50) return; // refuse obviously broken data

            lock (SaveLock)
            {
                // Newest data wins, so a broken or older remote file never replaces newer local data.
                if (string.CompareOrdinal(fresh.Updated, _data.Updated) < 0) return;
                string path = Path.Combine(_modDir, LocalFile);
                string? old = File.Exists(path) ? File.ReadAllText(path) : null;
                if (old != json) File.WriteAllText(path, json, new UTF8Encoding(false));
                _data = fresh;
            }
            GD.Print($"[StsTierOverlay] Tiers synced from GitHub ({fresh.Updated}): {fresh.Cards.Count} cards, {fresh.Relics.Count} relics.");
            WriteStatus();
        }
        catch (Exception e)
        {
            GD.Print($"[StsTierOverlay] GitHub sync skipped ({e.GetType().Name}); using {_data.Source}.");
        }
    }

    // ---------------------------------------------------------------- watcher

    private static void OnNodeAdded(Node node)
    {
        switch (node)
        {
            case NCard card:
                // Grids and inspect screens reuse card nodes and swap their model (e.g. scrolling the deck, paging
                // through cards). The game clears ModelChanged when a card goes back to its pool, so subscribe again
                // every time the card enters the tree (removing first so we never subscribe twice).
                Action<CardModel?> onChange = CardHandlers.GetValue(card, c =>
                    _ => Callable.From(() => Safe(() => EvaluateCard(c, refresh: true))).CallDeferred());
                card.ModelChanged -= onChange;
                card.ModelChanged += onChange;
                // Parent chain and Model are complete by the next idle step.
                Callable.From(() => Safe(() => EvaluateCard(card, refresh: false))).CallDeferred();
                break;
            case NRelic relic:
                if (!relic.HasMeta(WatchMeta))
                {
                    // Some holders (treasure chest) create the node first and set the relic later.
                    relic.SetMeta(WatchMeta, true);
                    relic.ModelChanged += (_, _) => Callable.From(() => Safe(() => EvaluateRelic(relic, refresh: true))).CallDeferred();
                }
                Callable.From(() => Safe(() => EvaluateRelic(relic, refresh: false))).CallDeferred();
                break;
            default:
                if (node is not Control control) break;
                string typeName = node.GetType().Name;
                if (typeName.Contains("RewardButton"))
                    Callable.From(() => Safe(() => RewardButtonHook.Evaluate(control))).CallDeferred();
                else if (typeName.Contains("EventOptionButton"))
                    Callable.From(() => Safe(() => EventOptionHook.Evaluate(control))).CallDeferred();
                break;
        }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NCard, Action<CardModel?>> CardHandlers = new();

    private static void Safe(Action action)
    {
        try { action(); }
        catch (Exception e) { GD.PrintErr($"[StsTierOverlay] {e.GetType().Name}: {e.Message}"); }
    }

    private static void EvaluateCard(NCard card, bool refresh)
    {
        if (!GodotObject.IsInstanceValid(card) || !card.IsInsideTree()) return;
        if (refresh) Badges.Remove(card);
        else if (Badges.Has(card)) return;

        TierData data = _data;
        if (card.Model == null) return;
        // Offers (rewards, shop): every card gets a badge, "?" if Mobalytics has no rating (e.g. colorless cards).
        // Browsing (deck, piles, inspect, tooltips): only rated cards, so Strikes and Defends stay clean.
        bool offer = InScreen(card, data.CardScreens);
        if (!offer && !InScreen(card, data.CardBrowseScreens)) return;
        string? tier = TierData.Lookup(data.Cards, card.Model.GetType().Name) ?? (offer ? "?" : null);
        if (tier != null) Badges.AddToCard(card, tier);
    }

    private static void EvaluateRelic(NRelic relic, bool refresh)
    {
        if (!GodotObject.IsInstanceValid(relic) || !relic.IsInsideTree()) return;
        if (refresh) Badges.Remove(relic);
        else if (Badges.Has(relic)) return;

        RelicModel model;
        try { model = relic.Model; }
        catch (InvalidOperationException) { return; } // relic not assigned yet; ModelChanged will call us again

        TierData data = _data;
        if (!InScreen(relic, data.RelicScreens)) return;
        Badges.AddToRelic(relic, RelicTier(model) ?? "?");
    }

    internal static string? RelicTier(RelicModel model) => TierData.Lookup(_data.Relics, model.GetType().Name);

    private static bool InScreen(Node node, string[] patterns)
    {
        for (Node? n = node.GetParent(); n != null; n = n.GetParent())
        {
            string typeName = n.GetType().Name;
            foreach (string p in patterns)
            {
                if (typeName.Contains(p, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
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
                Report.Add($"OK   {type.Name}");
            }
            catch (Exception e)
            {
                Report.Add($"FAIL {type.Name}: {e.Message}");
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
            Type[] types = typeof(CardModel).Assembly.GetTypes().Where(t => !t.IsAbstract).ToArray();
            string[] gameCards = types.Where(t => typeof(CardModel).IsAssignableFrom(t)).Select(t => t.Name).ToArray();
            string[] gameRelics = types.Where(t => typeof(RelicModel).IsAssignableFrom(t)).Select(t => t.Name).ToArray();
            List<string> unknownCards = TierData.Unknown(data.CardNames, gameCards);
            List<string> unknownRelics = TierData.Unknown(data.RelicNames, gameRelics);

            var sb = new StringBuilder();
            sb.AppendLine($"Card & Relic Tier Overlay v{Version} - {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Data: {data.Source} (updated {data.Updated})");
            sb.AppendLine($"Cards:  {data.Cards.Count} with a tier, {gameCards.Length} in the game. Screens: {string.Join(", ", data.CardScreens)}");
            sb.AppendLine($"Relics: {data.Relics.Count} with a tier, {gameRelics.Length} in the game. Screens: {string.Join(", ", data.RelicScreens)} + reward list");
            sb.AppendLine();
            sb.AppendLine("Components:");
            foreach (string line in Report) sb.AppendLine("  " + line);
            sb.AppendLine();
            sb.AppendLine($"Tier list names that do not exist in this game version:");
            sb.AppendLine($"  cards ({unknownCards.Count}):  " + (unknownCards.Count == 0 ? "(none)" : string.Join(", ", unknownCards)));
            sb.AppendLine($"  relics ({unknownRelics.Count}): " + (unknownRelics.Count == 0 ? "(none)" : string.Join(", ", unknownRelics)));
            File.WriteAllText(Path.Combine(_modDir, StatusFile), sb.ToString(), new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            GD.PrintErr($"[StsTierOverlay] Could not write {StatusFile}: {e.Message}");
        }
    }
}

/// <summary>
/// Relic entries in the post-combat reward list. Kept in its own class so that, if a game update renames these
/// types, only this feature stops working (the type load error is caught by the caller).
/// </summary>
internal static class RewardButtonHook
{
    private static readonly AccessTools.FieldRef<NRewardButton, Control>? IconContainer =
        AccessTools.Field(typeof(NRewardButton), "_iconContainer") != null
            ? AccessTools.FieldRefAccess<NRewardButton, Control>("_iconContainer")
            : null;

    internal static void Evaluate(Control node)
    {
        if (node is not NRewardButton button || !GodotObject.IsInstanceValid(button) || !button.IsInsideTree()) return;
        if (Badges.Has(button) || button.Reward is not RelicReward reward || reward.Relic == null) return;
        string tier = TierOverlay.RelicTier(reward.Relic) ?? "?";
        Control? icon = IconContainer?.Invoke(button);
        Badges.AddToRewardButton(button, icon, tier);
    }
}

/// <summary>
/// Event options that grant a relic, e.g. Neow and the other Ancients at the start of each act. Their tooltip
/// leaves the relic itself out, so the badge goes on the option's relic icon. Own class for the same reason as
/// <see cref="RewardButtonHook"/>.
/// </summary>
internal static class EventOptionHook
{
    internal static void Evaluate(Control node)
    {
        if (node is not MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton button) return;
        if (!GodotObject.IsInstanceValid(button) || !button.IsInsideTree() || button.Option?.Relic == null) return;
        string? tier = TierOverlay.RelicTier(button.Option.Relic);
        if (tier == null) return;
        if (button.GetNodeOrNull<TextureRect>("%RelicIcon") is TextureRect icon && icon.Visible)
        {
            if (!Badges.Has(icon)) Badges.AddAnchored(icon, tier, 30f, -10f);
        }
        else if (!Badges.Has(button))
        {
            Badges.AddAnchored(button, tier, 30f, 6f);
        }
    }
}
