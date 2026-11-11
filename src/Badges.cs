using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace StsTierOverlay;

/// <summary>Creates and removes the small tier badges.</summary>
internal static class Badges
{
    private const string BadgeMeta = "moba_tier_badge";

    private static readonly Dictionary<string, Color> TierColors = new()
    {
        ["S"] = new Color("ff4d4d"),
        ["A"] = new Color("ff9a2e"),
        ["B"] = new Color("ffd93b"),
        ["C"] = new Color("6fd65a"),
        ["D"] = new Color("59a8ff"),
        ["?"] = new Color("9a9a9a"), // offered but not rated by Mobalytics (e.g. colorless cards)
    };

    private static Font? _font; // the game's font, borrowed from the first card title we see

    internal static bool Has(Node host) =>
        host.GetChildren().OfType<Control>().Any(c => c.HasMeta(BadgeMeta) && !c.IsQueuedForDeletion());

    internal static void Remove(Node host)
    {
        foreach (Control c in host.GetChildren().OfType<Control>().Where(c => c.HasMeta(BadgeMeta)))
            c.QueueFree();
    }

    /// <summary>Card badge, pinned on the top-right corner of the frame (the energy cost sits top-left).</summary>
    internal static void AddToCard(NCard card, string tier)
    {
        if (_font == null && card.FindChild("TitleLabel", true, false) is Label title) _font = title.GetThemeFont("font");

        Vector2 corner = new Vector2(NCard.defaultSize.X / 2f, -NCard.defaultSize.Y / 2f); // NCard is centered
        if (card.GetNodeOrNull<Control>("%Frame") is Control frame)
        {
            Vector2? p = LocalCorner(card, frame, topRight: true);
            if (p is Vector2 v && v.X > 0f && Mathf.Abs(v.X) < 400f && Mathf.Abs(v.Y) < 400f) corner = v;
        }
        const float size = 46f;
        Attach(card, tier, size, corner + new Vector2(-size / 2f - 4f, -size / 2f + 2f));
    }

    /// <summary>Relic badge, on the top-right corner of the relic icon.</summary>
    internal static void AddToRelic(NRelic relic, string tier)
    {
        Control target = relic.Icon != null ? relic.Icon : relic;
        Vector2 corner = LocalCorner(relic, target, topRight: true) ?? new Vector2(relic.Size.X, 0f);
        const float size = 34f;
        Attach(relic, tier, size, corner + new Vector2(-size * 0.55f, -size * 0.45f));
    }

    /// <summary>
    /// Badge pinned to the top-right corner of <paramref name="target"/> with anchors, so it follows later resizes.
    /// Used for tooltips and event option icons. Containers lay out their children, so if the target is a
    /// container the badge goes on its first non-container child that covers it (e.g. a tooltip's background).
    /// </summary>
    internal static void AddAnchored(Control target, string tier, float size, float inset)
    {
        Control host = target;
        if (host is Container)
        {
            Control? free = host.GetChildren().OfType<Control>().FirstOrDefault(c => c is not Container && c.Visible);
            if (free == null) return;
            host = free;
        }
        Panel panel = Attach(host, tier, size, Vector2.Zero);
        panel.AnchorLeft = 1f;
        panel.AnchorRight = 1f;
        panel.AnchorTop = 0f;
        panel.AnchorBottom = 0f;
        panel.OffsetLeft = -size - inset;
        panel.OffsetRight = -inset;
        panel.OffsetTop = inset;
        panel.OffsetBottom = inset + size;
    }

    /// <summary>Badge on a reward-list button, on the top-right corner of its icon.</summary>
    internal static void AddToRewardButton(Control button, Control? icon, string tier)
    {
        Vector2 corner = icon != null ? LocalCorner(button, icon, topRight: true) ?? Vector2.Zero : new Vector2(button.Size.X, 0f);
        const float size = 30f;
        Attach(button, tier, size, corner + new Vector2(-size * 0.55f, -size * 0.45f));
    }

    /// <summary>Corner of <paramref name="target"/>'s rect, in <paramref name="host"/>'s local coordinates.</summary>
    private static Vector2? LocalCorner(Control host, Control target, bool topRight)
    {
        try
        {
            if (!host.IsInsideTree() || !target.IsInsideTree()) return null;
            Vector2 global = target.GetGlobalTransform() * new Vector2(topRight ? target.Size.X : 0f, 0f);
            return host.GetGlobalTransform().AffineInverse() * global;
        }
        catch { return null; }
    }

    private static Panel Attach(Control host, string tier, float size, Vector2 position)
    {
        Color color = TierColors.TryGetValue(tier, out Color c) ? c : Colors.White;

        // Discreet look: dark disc, tier-colored ring and letter.
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.09f, 0.07f, 0.10f, 0.92f),
            BorderColor = color,
            ShadowColor = new Color(0, 0, 0, 0.45f),
            ShadowSize = 4,
            AntiAliasing = true,
        };
        style.SetBorderWidthAll(size >= 40f ? 3 : 2);
        style.SetCornerRadiusAll((int)(size / 2));

        var panel = new Panel
        {
            Name = "MobaTierBadge",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(size, size),
            Position = position,
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
        label.AddThemeFontSizeOverride("font_size", (int)Math.Round(size * 0.61f));
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        label.AddThemeConstantOverride("outline_size", size >= 40f ? 6 : 5);
        if (_font != null) label.AddThemeFontOverride("font", _font);
        panel.AddChild(label);

        host.AddChild(panel);

        // Nodes get pooled/reused (e.g. cards in the combat hand): drop the badge when the host leaves this screen.
        host.Connect(Node.SignalName.TreeExiting, Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(panel)) panel.QueueFree();
        }), (uint)GodotObject.ConnectFlags.OneShot);
        return panel;
    }
}
