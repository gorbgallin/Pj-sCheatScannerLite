using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DetectorLite
{
    class Program
    {
        static void Main(string[] args)
        {
            // === .NET 8 RUNTIME CHECK ===
            if (!IsDotNet8RuntimeInstalled())
            {
                Console.WriteLine("============================================");
                Console.WriteLine("  .NET 8.0 Desktop Runtime NOT FOUND");
                Console.WriteLine("============================================");
                Console.WriteLine();
                Console.WriteLine("This tool requires the .NET 8.0 Desktop Runtime.");
                Console.WriteLine();
                Console.Write("Would you like to install it now? [Y/n]: ");
                var choice = Console.ReadLine()?.Trim().ToLower();
                if (choice == "n" || choice == "no")
                {
                    Console.WriteLine();
                    Console.WriteLine("Install cancelled. You can download it manually from:");
                    Console.WriteLine("https://dotnet.microsoft.com/download/dotnet/8.0");
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey(true);
                    return;
                }

                // Try winget first
                Console.WriteLine();
                Console.WriteLine("Checking for Windows Package Manager (winget)...");
                if (IsWingetAvailable())
                {
                    Console.WriteLine("Found winget. Installing .NET 8 Desktop Runtime...");
                    Console.WriteLine("This may take a few minutes... Please wait.");
                    var wingetPsi = new ProcessStartInfo("winget", "install Microsoft.DotNet.DesktopRuntime.8 --silent --accept-source-agreements --accept-package-agreements")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    var wingetProc = Process.Start(wingetPsi);
                    wingetProc?.WaitForExit();
                    if (wingetProc?.ExitCode == 0)
                    {
                        Console.WriteLine();
                        Console.WriteLine(".NET 8 installed via winget!");
                        Console.WriteLine("You may need to restart this tool if it still fails.");
                        Console.WriteLine();
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadKey(true);
                        return;
                    }
                }

                // Fall back to direct download
                Console.WriteLine();
                Console.WriteLine("Downloading .NET 8 Desktop Runtime installer...");
                var installerPath = Path.Combine(Path.GetTempPath(), "dotnet8-runtime-installer.exe");
                try
                {
                    var dlPsi = new ProcessStartInfo("powershell",
                        $"-Command \"Invoke-WebRequest -Uri 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe' -OutFile '{installerPath}' -UseBasicParsing\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    var dlProc = Process.Start(dlPsi);
                    dlProc?.WaitForExit();

                    if (File.Exists(installerPath))
                    {
                        Console.WriteLine("Running installer silently...");
                        var runPsi = new ProcessStartInfo(installerPath, "/install /quiet /norestart")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        var runProc = Process.Start(runPsi);
                        runProc?.WaitForExit();
                        try { File.Delete(installerPath); } catch { }

                        Console.WriteLine();
                        Console.WriteLine("Installer finished. You may need to restart your PC.");
                        Console.WriteLine();
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadKey(true);
                        return;
                    }
                }
                catch { }

                Console.WriteLine();
                Console.WriteLine("[ERROR] Failed to download the installer automatically.");
                Console.WriteLine("Please install manually from: https://dotnet.microsoft.com/download/dotnet/8.0");
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
                return;
            }

            // Admin check
            if (!IsRunningAsAdmin())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("WARNING: Run as Administrator for full memory access.");
                Console.ResetColor();
                Console.WriteLine();
            }

            Console.WriteLine("==============================================");
            Console.WriteLine("  PjCheatScanner - LITE");
            Console.WriteLine("  I hate dirty little cheaters!");
            Console.WriteLine("==============================================");
            Console.WriteLine();

            var results = new DetectionResults();

            // === MEMORY SCAN ===
            var targets = new[] { "javaw", "java", "minecraft" };
            var processes = targets.SelectMany(Process.GetProcessesByName).ToList();

            if (processes.Count == 0)
            {
                Console.WriteLine("No Minecraft/Java processes found.");
            }

            else
            {
                foreach (var proc in processes)
                {
                    try
                    {
                        results.ScannedProcesses.Add(proc.Id);
                        var hits = MemoryScannerLite.ScanProcess(proc);
                        if (hits.Count > 0)
                        {
                            results.MemoryHits[proc.Id] = hits;
                        }
                    }
                    catch { }
                    finally { try { proc.Dispose(); } catch { } }
                }
            }

            Console.WriteLine("\nDo you want to run the prefetch checker? y/n");
            var prefetchChoice = Console.ReadLine()?.Trim().ToLower();
            if (prefetchChoice == "y" || prefetchChoice == "")
            {
                Console.WriteLine("\nScanning Windows Prefetch for weird executions...");
                results.PrefetchEntries = PrefetchViewer.Check();
                foreach (var p in results.PrefetchEntries)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [PREFETCH] {p.ExecutableName}  --  {p.Reason}");
                    Console.ResetColor();
                }
                if (results.PrefetchEntries.Count == 0)
                {
                    Console.WriteLine("  No suspicious prefetch entries found!");
                }
            }

            // === RECENT FILES / TIMESTAMPS ===

            Console.WriteLine("\nChecking recent suspicious files...");
            results.RecentFiles = RecentFilesChecker.Check();
            foreach (var f in results.RecentFiles)
            {
                Console.WriteLine($"  [FILE] {f}");
            }
            if (results.RecentFiles.Count == 0)
            {
                Console.WriteLine("  No suspicious recent files found.");
            }

            // === SUMMARY ===
            Console.WriteLine("\n--- SUMMARY ---");
            Console.WriteLine($"Processes scanned: {results.ScannedProcesses.Count}");
            Console.WriteLine($"Memory hits: {results.MemoryHits.Values.Sum(v => v.Count)}");

            Console.WriteLine($"Suspicious files: {results.RecentFiles.Count}");
            Console.WriteLine($"Prefetch anomalies: {results.PrefetchEntries.Count}");

            bool anyHits = results.MemoryHits.Any()
                        || results.RecentFiles.Any()
                        || results.PrefetchEntries.Any();


            if (anyHits)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("RESULT: POTENTIAL CHEAT DETECTED");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("RESULT: Clean");
                Console.ResetColor();
            }

            // Save JSON report
            string json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("pjcheatscanner_lite_report.json", json);
            Console.WriteLine("\nReport saved: pjcheatscanner_lite_report.json");

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey(true);
        }

        private static bool IsRunningAsAdmin()
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        private static bool IsDotNet8RuntimeInstalled()
        {
            try
            {
                var psi = new ProcessStartInfo("dotnet", "--list-runtimes")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                if (proc == null) return false;
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                return output.Contains("Microsoft.WindowsDesktop.App 8.0");
            }
            catch
            {
                return false;
            }
        }

        private static bool IsWingetAvailable()
        {
            try
            {
                var psi = new ProcessStartInfo("where", "winget")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit();
                return proc?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }


    public class DetectionResults
    {
        public List<int> ScannedProcesses { get; set; } = new();
        public Dictionary<int, List<string>> MemoryHits { get; set; } = new();
        public List<string> RecentFiles { get; set; } = new();
        public List<PrefetchEntry> PrefetchEntries { get; set; } = new();
    }
}

