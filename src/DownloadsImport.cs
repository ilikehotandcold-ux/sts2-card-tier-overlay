using System;
using System.IO;
using System.Linq;
using Godot;
using Environment = System.Environment;

namespace StsTierOverlay;

/// <summary>
/// Offline fallback: at startup, picks up the newest sts2-tiers*.json that tools/extract-tiers.js saved to the
/// Downloads folder, if it is newer than the data the mod already has. Normally tiers arrive from GitHub.
/// </summary>
internal static class DownloadsImport
{
    private const string ImportStampFile = "last_import.txt";

    internal static void Run()
    {
        string? modDir = TierOverlay.ModDir;
        string? downloads = DownloadsDir();
        if (modDir == null || downloads == null || !Directory.Exists(downloads)) return;

        DateTime last = ReadStamp(modDir);
        FileInfo? newest = new DirectoryInfo(downloads)
            .GetFiles(TierOverlay.DownloadFilePattern)
            .Where(f => f.LastWriteTimeUtc > last)
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();
        if (newest == null) return;

        WriteStamp(modDir, newest.LastWriteTimeUtc);
        try
        {
            string json = File.ReadAllText(newest.FullName);
            var data = TierOverlay.ParseFile(json, "Downloads (" + newest.Name + ")");
            bool applied = TierOverlay.TryApply(json, data);
            GD.Print($"[StsTierOverlay] {newest.Name}: {(applied ? "imported" : "not newer than current data, ignored")}.");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[StsTierOverlay] {newest.Name} is not valid: {e.Message}");
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
}
