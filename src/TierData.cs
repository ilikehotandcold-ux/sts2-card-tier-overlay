using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StsTierOverlay;

/// <summary>
/// Parsed contents of data/tiers.json:
/// <code>
/// {
///   "_updated": "2026-11-11",
///   "_screens": { "cards": ["CardReward", ...], "cardsBrowse": ["DeckView", ...], "relics": ["TreasureRoomRelic", ...] },
///   "cards":  { "Ironclad": { "S": ["offering", ...], ... }, ... },
///   "relics": { "S": ["pocketwatch", ...], ... }
/// }
/// </code>
/// Inside "cards" and "relics", any nesting is allowed: every array under a tier-letter key is read.
/// </summary>
internal sealed class TierData
{
    public static readonly string[] DefaultCardScreens = { "CardReward", "ChooseACard", "Merchant" };
    public static readonly string[] DefaultRelicScreens = { "TreasureRoomRelic", "ChooseARelic", "Merchant" };

    /// <summary>Screens where you look at cards you already have: badges only for rated cards, no "?".</summary>
    public static readonly string[] DefaultCardBrowseScreens =
    {
        "DeckView", "CardPileScreen", "InspectCard", "DeckCardSelect", "DeckUpgradeSelect",
        "DeckTransformSelect", "DeckEnchantSelect", "CombatPileCardSelect", "HoverTipSet",
    };

    public readonly Dictionary<string, string> Cards = new();   // normalized name -> "S".."D"
    public readonly Dictionary<string, string> Relics = new();
    public readonly List<string> CardNames = new();             // original slugs, for the health report
    public readonly List<string> RelicNames = new();
    public string[] CardScreens = DefaultCardScreens;          // offers: unrated cards get a "?" badge
    public string[] CardBrowseScreens = DefaultCardBrowseScreens;
    public string[] RelicScreens = DefaultRelicScreens;
    public string Updated = "";                                 // "yyyy-MM-dd"; the newest data wins
    public string Source = "";

    public static TierData Parse(string json, string source)
    {
        var data = new TierData { Source = source };
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("_updated", out JsonElement updated) && updated.ValueKind == JsonValueKind.String)
            data.Updated = updated.GetString() ?? "";

        if (root.TryGetProperty("_screens", out JsonElement screens) && screens.ValueKind == JsonValueKind.Object)
        {
            data.CardScreens = ReadPatterns(screens, "cards") ?? DefaultCardScreens;
            data.CardBrowseScreens = ReadPatterns(screens, "cardsBrowse") ?? DefaultCardBrowseScreens;
            data.RelicScreens = ReadPatterns(screens, "relics") ?? DefaultRelicScreens;
        }

        if (root.TryGetProperty("cards", out JsonElement cards)) Collect(cards, null, data.Cards, data.CardNames);
        if (root.TryGetProperty("relics", out JsonElement relics)) Collect(relics, null, data.Relics, data.RelicNames);

        if (data.Cards.Count == 0 && data.Relics.Count == 0) throw new InvalidDataException("no tiers found");
        return data;
    }

    private static string[]? ReadPatterns(JsonElement screens, string key)
    {
        if (!screens.TryGetProperty(key, out JsonElement arr) || arr.ValueKind != JsonValueKind.Array) return null;
        string[] patterns = arr.EnumerateArray().Select(e => e.GetString() ?? "").Where(p => p.Length > 0).ToArray();
        return patterns.Length > 0 ? patterns : null;
    }

    private static void Collect(JsonElement node, string? tier, Dictionary<string, string> into, List<string> names)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty p in node.EnumerateObject())
            {
                if (p.Name.StartsWith("_")) continue;
                bool isTier = p.Name.Length == 1 && "SABCDEF".Contains(char.ToUpperInvariant(p.Name[0]));
                Collect(p.Value, isTier ? p.Name.ToUpperInvariant() : tier, into, names);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array && tier != null)
        {
            foreach (JsonElement e in node.EnumerateArray())
            {
                string slug = e.GetString() ?? "";
                if (slug.EndsWith("-plus")) slug = slug[..^5];
                if (slug.Length > 0 && into.TryAdd(Normalize(slug), tier)) names.Add(slug);
            }
        }
    }

    public static string Normalize(string s) =>
        new string(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>Tier for a game class name, tolerating a leading "the" on either side (TheAbacus, ChosenCheese).</summary>
    public static string? Lookup(Dictionary<string, string> tiers, string className)
    {
        string key = Normalize(className);
        if (tiers.TryGetValue(key, out string? t)) return t;
        if (tiers.TryGetValue("the" + key, out t)) return t;
        if (key.StartsWith("the") && tiers.TryGetValue(key[3..], out t)) return t;
        return null;
    }

    /// <summary>Slugs from the tier list that match none of the given game class names.</summary>
    public static List<string> Unknown(IEnumerable<string> slugs, IEnumerable<string> classNames)
    {
        var game = new HashSet<string>(classNames.Select(Normalize));
        return slugs.Where(s =>
        {
            string k = Normalize(s);
            return !game.Contains(k) && !game.Contains("the" + k) && !(k.StartsWith("the") && game.Contains(k[3..]));
        }).ToList();
    }
}
