using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Saves;

namespace StsTierOverlay;

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
