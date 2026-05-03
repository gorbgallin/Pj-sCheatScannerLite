// RecentFilesChecker.cs - Checks recent executable timestamps in common locations
// Looks for recently created/modified .exe/.dll/.jar files that may be cheat injectors
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DetectorLite
{
    public static class RecentFilesChecker
    {
        private static readonly string[] SearchPaths = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft"),
            Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };

        private static readonly string[] SuspiciousExts = { ".dll", ".exe", ".jar" };
        private static readonly TimeSpan RecentThreshold = TimeSpan.FromDays(1);

        private static readonly string[] SuspiciousKeywords = {
            "ghost", "inject", "hack", "argon", "bypass", "macro", "aimbot",
            "crystal", "anchor", "totem", "aura", "triggerbot", "aimassist", "autoclicker",
            "shieldbreak", "selfdestruct", "s3lfd3struct", "prestige", "vape", "doomsdayclient",

        };

        public static List<string> Check()
        {
            var suspicious = new List<string>();
            foreach (var path in SearchPaths)
            {
                if (!Directory.Exists(path)) continue;

                try
                {
                    var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                        .Where(f => SuspiciousExts.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                        .Where(f =>
                        {
                            try { return (DateTime.Now - File.GetLastWriteTime(f)) < RecentThreshold; }
                            catch { return false; }
                        })
                        .Where(f => SuspiciousKeywords.Any(kw =>
                            Path.GetFileNameWithoutExtension(f).ToLower().Contains(kw)));

                    foreach (var file in files)
                    {
                        try
                        {
                            var fi = new FileInfo(file);
                            suspicious.Add($"{file} (modified: {fi.LastWriteTime})");
                        }
                        catch { }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (Exception) { }
            }
            return suspicious;
        }

        // Open/show a folder or file timestamp externally (Explorer)
        public static void OpenContainingFolder(string filePath)
        {
            if (File.Exists(filePath))
            {
                var psi = new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"")
                {
                    UseShellExecute = false
                };
                System.Diagnostics.Process.Start(psi);
            }
        }
    }
}

