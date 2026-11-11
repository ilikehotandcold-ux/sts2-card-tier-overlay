using System;
using System.IO;
using System.Linq;
using Godot;
using Environment = System.Environment;

namespace StsTierOverlay;

/// <summary>
/// "Update tiers" button on the main menu.
///
/// Mobalytics blocks automated clients, so the update is done by the player in their own browser:
/// the button opens the tier list page, the player clicks the "Update STS2 tiers" bookmarklet, which
/// downloads sts2-tiers-YYYY-MM-DD.json, and this class picks that file up from the Downloads folder.
/// </summary>
internal static class TierUpdater
{
    private const string ImportStampFile = "last_import.txt";
    private static readonly TimeSpan WaitFor = TimeSpan.FromMinutes(10);

    private static Button? _button;
    private static DateTime _waitUntil = DateTime.MinValue;
    private static bool _polling;
    private const string IdleText = "Update tiers";

    // ---------------------------------------------------------------- import

    /// <summary>
    /// Imports the newest sts2-tiers*.json from Downloads that was not imported before.
    /// <paramref name="manual"/>: the player asked for it, so it wins even if its date is older.
    /// </summary>
    internal static bool ImportFromDownloads(bool manual = false)
    {
        string? modDir = TierOverlay.ModDir;
        string? downloads = DownloadsDir();
        if (modDir == null || downloads == null || !Directory.Exists(downloads)) return false;

        DateTime last = ReadStamp(modDir);
        FileInfo? newest = new DirectoryInfo(downloads)
            .GetFiles(TierOverlay.DownloadFilePattern)
            .Where(f => f.LastWriteTimeUtc > last)
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();
        if (newest == null) return false;

        string json;
        try { json = File.ReadAllText(newest.FullName); }
        catch (IOException) { return false; } // browser still writing it; try again on the next tick

        WriteStamp(modDir, newest.LastWriteTimeUtc);
        try
        {
            var data = TierOverlay.ParseFile(json, "Mobalytics (" + newest.Name + ")");
            bool applied = TierOverlay.TryApply(json, data, force: manual);
            GD.Print($"[StsTierOverlay] {newest.Name}: {(applied ? "imported" : "older than current data, ignored")}.");
            return applied;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[StsTierOverlay] {newest.Name} is not valid: {e.Message}");
            return false;
        }
    }

    private static string? DownloadsDir()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders");
            if (key?.GetValue("{374DE290-123F-4565-9164-39C4925E467B}") is string p && p.Length > 0)
                return Environment.ExpandEnvironmentVariables(p);
        }
        catch { /* fall back below */ }
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 ? Path.Combine(home, "Downloads") : null;
    }

    private static DateTime ReadStamp(string modDir)
    {
        try
        {
            string path = Path.Combine(modDir, ImportStampFile);
            if (File.Exists(path) && long.TryParse(File.ReadAllText(path).Trim(), out long ticks))
                return new DateTime(ticks, DateTimeKind.Utc);
        }
        catch { /* treat as never imported */ }
        return DateTime.MinValue;
    }

    private static void WriteStamp(string modDir, DateTime utc)
    {
        try { File.WriteAllText(Path.Combine(modDir, ImportStampFile), utc.Ticks.ToString()); }
        catch { /* not critical */ }
    }

    // ---------------------------------------------------------------- button

    internal static void AddButton(Control menu)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(menu) || menu.GetNodeOrNull("MobaTierUpdateButton") != null) return;

            var button = new Button
            {
                Name = "MobaTierUpdateButton",
                Text = IdleText,
                FocusMode = Control.FocusModeEnum.None,
                AnchorLeft = 0, AnchorRight = 0, AnchorTop = 1, AnchorBottom = 1,
                OffsetLeft = 28, OffsetRight = 28 + 300, OffsetTop = -86, OffsetBottom = -32,
                TooltipText = "Opens the Mobalytics tier list. Click the \"Update STS2 tiers\" bookmark there and the game loads the new tiers.",
            };
            button.AddThemeStyleboxOverride("normal", Box(new Color(0.10f, 0.08f, 0.12f, 0.80f)));
            button.AddThemeStyleboxOverride("hover", Box(new Color(0.22f, 0.17f, 0.10f, 0.92f)));
            button.AddThemeStyleboxOverride("pressed", Box(new Color(0.30f, 0.22f, 0.10f, 0.95f)));
            button.AddThemeColorOverride("font_color", new Color("f1e9da"));
            button.AddThemeColorOverride("font_hover_color", new Color("ffd98a"));
            button.AddThemeFontSizeOverride("font_size", 22);
            Font? font = (menu.FindChildren("*", "Label", true, false).FirstOrDefault() as Label)?.GetThemeFont("font");
            if (font != null) button.AddThemeFontOverride("font", font);

            button.Pressed += OnPressed;
            menu.AddChild(button);
            _button = button;
            if (_polling) SetText("Waiting for Mobalytics...");

            // Hide it together with the main menu buttons when a submenu is open.
            if (menu.GetNodeOrNull<Control>("MainMenuTextButtons") is Control mainButtons)
            {
                button.Visible = mainButtons.Visible;
                mainButtons.VisibilityChanged += () =>
                {
                    if (GodotObject.IsInstanceValid(button) && GodotObject.IsInstanceValid(mainButtons))
                        button.Visible = mainButtons.Visible;
                };
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[StsTierOverlay] Could not add the update button: {e.Message}");
        }
    }

    private static StyleBoxFlat Box(Color bg)
    {
        var box = new StyleBoxFlat { BgColor = bg, BorderColor = new Color("e8b64c") };
        box.SetBorderWidthAll(2);
        box.SetCornerRadiusAll(10);
        box.ContentMarginLeft = box.ContentMarginRight = 14;
        return box;
    }

    private static void OnPressed()
    {
        string? modDir = TierOverlay.ModDir;
        string helper = modDir == null ? "" : Path.Combine(modDir, TierOverlay.HelperPageFile);
        bool firstTime = modDir == null || !File.Exists(Path.Combine(modDir, TierOverlay.BookmarkOk));

        // First time: page that explains how to install the bookmarklet. Afterwards: straight to Mobalytics.
        OS.ShellOpen(firstTime && File.Exists(helper) ? helper : TierOverlay.MobalyticsPage);

        _waitUntil = DateTime.UtcNow + WaitFor;
        SetText("Waiting for Mobalytics...");
        if (!_polling)
        {
            _polling = true;
            ScheduleTick();
        }
    }

    private static void ScheduleTick()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.CreateTimer(2.0).Timeout += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (ImportFromDownloads(manual: true))
            {
                _polling = false;
                string? modDir = TierOverlay.ModDir;
                if (modDir != null) File.WriteAllText(Path.Combine(modDir, TierOverlay.BookmarkOk), DateTime.Now.ToString("s"));
                SetText($"Updated: {TierOverlay.CardCount} cards");
                ResetTextLater();
                return;
            }
            if (DateTime.UtcNow > _waitUntil)
            {
                _polling = false;
                SetText("Nothing arrived. Try again");
                ResetTextLater();
                return;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[StsTierOverlay] Update check failed: {e.Message}");
        }
        ScheduleTick();
    }

    private static void ResetTextLater()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.CreateTimer(8.0).Timeout += () => { if (!_polling) SetText(IdleText); };
    }

    private static void SetText(string text)
    {
        if (_button != null && GodotObject.IsInstanceValid(_button)) _button.Text = text;
    }
}
