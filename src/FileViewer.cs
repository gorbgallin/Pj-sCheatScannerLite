// FileViewer.cs - Unified file analysis component
// Combines PrefetchViewer and RecentFilesChecker with enhanced detection
// Including prefetch parsing, recent files scanning, and DLL injection detection
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DetectorLite
{
    public static class FileViewer
    {
        private static readonly string[] SearchPaths = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft"),
            Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };

        private static readonly string[] SuspiciousExts = { ".dll", ".exe", ".jar" };
        private static readonly TimeSpan RecentThreshold = TimeSpan.FromDays(1);
        private static readonly TimeSpan PrefetchThreshold = TimeSpan.FromDays(7);

        private static readonly HashSet<string> _cheatKeywords = LoadCheatStrings();

        private static HashSet<string> LoadCheatStrings()
        {
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] candidates = {
                Path.Combine(AppContext.BaseDirectory, "cheat_strings.txt"),
                "cheat_strings.txt"
            };

            foreach (var path in candidates)
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    foreach (var line in File.ReadAllLines(path))
                    {
                        var trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            keywords.Add(trimmed.ToLower());
                    }
                    if (keywords.Count > 0) break;
                }
                catch { }
            }

            return keywords;
        }

        public static List<FileViewerEntry> Check()
        {
            var entries = new List<FileViewerEntry>();

            // Prefetch scanning
            entries.AddRange(CheckPrefetch());

            // Recent files scanning
            entries.AddRange(CheckRecentFiles());

            // DLL injection detection (requires admin)
            try
            {
                entries.AddRange(CheckInjectedDLLs());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  DLL injection check failed: {ex.Message}");
            }

            // Cross-reference and assign confidence levels
            AssignConfidenceLevels(entries);

            return entries;
        }

        private static List<FileViewerEntry> CheckPrefetch()
        {
            var entries = new List<FileViewerEntry>();
            var prefetchDir = @"C:\Windows\Prefetch";
            if (!Directory.Exists(prefetchDir)) return entries;

            foreach (var file in Directory.GetFiles(prefetchDir, "*.pf"))
            {
                try
                {
                    var filename = Path.GetFileNameWithoutExtension(file);
                    var exeName = filename.Split('-')[0].ToLower();
                    var fileInfo = new FileInfo(file);

                    // Check if within threshold
                    if (DateTime.Now - fileInfo.LastWriteTime > PrefetchThreshold)
                        continue;

                    // Check if executable name contains cheat keywords
                    if (!_cheatKeywords.Any(kw => exeName.Contains(kw)))
                        continue;

                    // Check if it's a suspicious extension
                    if (!SuspiciousExts.Any(ext => exeName.EndsWith(ext)))
                        continue;

                    // Exclude known legitimate executables
                    if (exeName == "javaw" || exeName == "java" || exeName == "minecraftlauncher")
                        continue;

                    entries.Add(new FileViewerEntry
                    {
                        FilePath = file,
                        ExecutableName = exeName,
                        LastSeen = fileInfo.LastWriteTime,
                        Source = "Prefetch",
                        Reason = $"Prefetch entry for suspicious executable ({exeName})",
                        Confidence = ConfidenceLevel.Medium
                    });
                }
                catch { }
            }

            return entries;
        }

        private static List<FileViewerEntry> CheckRecentFiles()
        {
            var entries = new List<FileViewerEntry>();

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
                        .Where(f => _cheatKeywords.Any(kw =>
                            Path.GetFileNameWithoutExtension(f).ToLower().Contains(kw)));

                    foreach (var file in files)
                    {
                        try
                        {
                            var fi = new FileInfo(file);
                            var fileName = Path.GetFileNameWithoutExtension(file).ToLower();

                            entries.Add(new FileViewerEntry
                            {
                                FilePath = file,
                                ExecutableName = fileName,
                                LastSeen = fi.LastWriteTime,
                                Source = "RecentFile",
                                Reason = $"Recently modified suspicious file ({Path.GetExtension(file)})",
                                Confidence = ConfidenceLevel.Medium
                            });
                        }
                        catch { }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (Exception) { }
            }

            return entries;
        }

        private static List<FileViewerEntry> CheckInjectedDLLs()
        {
            var entries = new List<FileViewerEntry>();
            var targets = new[] { "javaw", "java", "minecraft" };
            var processes = targets.SelectMany(Process.GetProcessesByName).ToList();

            foreach (var proc in processes)
            {
                try
                {
                    foreach (ProcessModule module in proc.Modules)
                    {
                        try
                        {
                            var modulePath = module.FileName?.ToLower();
                            if (string.IsNullOrEmpty(modulePath)) continue;

                            var moduleName = Path.GetFileNameWithoutExtension(modulePath);

                            // Check if DLL is outside standard directories
                            var isOutsideStandard = !modulePath.Contains(".minecraft") &&
                                                  !modulePath.Contains("program files") &&
                                                  !modulePath.Contains("windows") &&
                                                  !modulePath.Contains("system32");

                            if (!isOutsideStandard) continue;

                            // Check if DLL name contains cheat keywords
                            if (!_cheatKeywords.Any(kw => moduleName.Contains(kw))) continue;

                            // Check if recently modified
                            var moduleInfo = new FileInfo(modulePath);
                            if (DateTime.Now - moduleInfo.LastWriteTime > RecentThreshold) continue;

                            entries.Add(new FileViewerEntry
                            {
                                FilePath = modulePath,
                                ExecutableName = moduleName,
                                LastSeen = moduleInfo.LastWriteTime,
                                Source = "InjectedDLL",
                                Reason = $"Suspicious DLL loaded in process {proc.ProcessName}.exe (PID {proc.Id})",
                                Confidence = ConfidenceLevel.High
                            });
                        }
                        catch { }
                    }
                }
                catch { }
                finally { try { proc.Dispose(); } catch { } }
            }

            return entries;
        }

        private static void AssignConfidenceLevels(List<FileViewerEntry> entries)
        {
            // Group by executable name
            var grouped = entries.GroupBy(e => e.ExecutableName.ToLower());

            foreach (var group in grouped)
            {
                var groupEntries = group.ToList();
                var sources = groupEntries.Select(e => e.Source).Distinct().ToList();

                // If appears in multiple sources, upgrade confidence
                if (sources.Count > 1)
                {
                    foreach (var entry in groupEntries)
                    {
                        entry.Confidence = ConfidenceLevel.High;
                        entry.Reason += $" [Cross-referenced: {string.Join(", ", sources)}]";
                    }
                }
            }
        }
    }

    public class FileViewerEntry
    {
        public string FilePath { get; set; }
        public string ExecutableName { get; set; }
        public DateTime LastSeen { get; set; }
        public string Source { get; set; } // "Prefetch", "RecentFile", "InjectedDLL"
        public string Reason { get; set; }
        public ConfidenceLevel Confidence { get; set; }
    }

    public enum ConfidenceLevel
    {
        Low,
        Medium,
        High
    }
}
