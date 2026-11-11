using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace StsTierOverlay;

/// <summary>
/// Adds the tier badge to relic tooltips, wherever the game shows them: your relic bar, chests, boss rewards,
/// the shop, events... NHoverTipSet.Init builds one tooltip control per text hover tip, in order, so the
/// N-th text tip maps to the N-th control it added. If a game update changes this method, the patch is
/// reported as FAIL in status.txt and everything else keeps working.
/// </summary>
[HarmonyPatch(typeof(NHoverTipSet), "Init", typeof(Control), typeof(IEnumerable<IHoverTip>))]
internal static class TooltipPatch
{
    private static readonly AccessTools.FieldRef<NHoverTipSet, Control>? TextContainer =
        AccessTools.Field(typeof(NHoverTipSet), "_textHoverTipContainer") != null
            ? AccessTools.FieldRefAccess<NHoverTipSet, Control>("_textHoverTipContainer")
            : null;

    // Materialize the tips once, so the game and our postfix enumerate the exact same list.
    private static void Prefix(ref IEnumerable<IHoverTip> hoverTips)
    {
        if (hoverTips is not List<IHoverTip>) hoverTips = hoverTips.ToList();
    }

    private static void Postfix(NHoverTipSet __instance, IEnumerable<IHoverTip> hoverTips)
    {
        try
        {
            Control? container = TextContainer?.Invoke(__instance);
            if (container == null) return;

            List<HoverTip> textTips = IHoverTip.RemoveDupes(hoverTips).OfType<HoverTip>().ToList();
            List<Control> controls = container.GetChildren().OfType<Control>().ToList();
            if (controls.Count < textTips.Count) return;
            controls = controls.Skip(controls.Count - textTips.Count).ToList();

            for (int i = 0; i < textTips.Count; i++)
            {
                if (textTips[i].CanonicalModel is not RelicModel relic) continue;
                string? tier = TierOverlay.RelicTier(relic);
                if (tier != null && !Badges.Has(controls[i])) Badges.AddAnchored(controls[i], tier, 30f, 10f);
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[StsTierOverlay] Tooltip badge: {e.Message}");
        }
    }
}
