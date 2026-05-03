using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

class Bootstrapper
{
    static void Main()
    {
        string appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string dllPath = Path.Combine(appDir, "PjCheatScannerLite.dll");

        if (!File.Exists(dllPath))
        {
            Console.WriteLine("[ERROR] PjCheatScannerLite.dll not found.");
            Console.WriteLine("Make sure you extracted ALL files from the zip.");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
            return;
        }

        // Check for .NET 8 Desktop Runtime
        if (!IsDotNet8RuntimeInstalled())
        {
            Console.WriteLine("============================================");
            Console.WriteLine("  .NET 8.0 Desktop Runtime NOT FOUND");
            Console.WriteLine("============================================");
            Console.WriteLine();
            Console.WriteLine("This tool requires the .NET 8.0 Desktop Runtime.");
            Console.WriteLine();
            Console.Write("Would you like to install it now? [Y/n]: ");
            string choice = Console.ReadLine();
            if (choice != null)
            {
                choice = choice.Trim().ToLower();
            }
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
                ProcessStartInfo wingetPsi = new ProcessStartInfo("winget", "install Microsoft.DotNet.DesktopRuntime.8 --silent --accept-source-agreements --accept-package-agreements")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process wingetProc = Process.Start(wingetPsi);
                if (wingetProc != null)
                {
                    wingetProc.WaitForExit();
                    if (wingetProc.ExitCode == 0)
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
            }

            // Fall back to direct download
            Console.WriteLine();
            Console.WriteLine("Downloading .NET 8 Desktop Runtime installer...");
            string installerPath = Path.Combine(Path.GetTempPath(), "dotnet8-runtime-installer.exe");
            try
            {
                ProcessStartInfo dlPsi = new ProcessStartInfo("powershell",
                    "-Command \"Invoke-WebRequest -Uri 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe' -OutFile '" + installerPath + "' -UseBasicParsing\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process dlProc = Process.Start(dlPsi);
                if (dlProc != null)
                {
                    dlProc.WaitForExit();
                }

                if (File.Exists(installerPath))
                {
                    Console.WriteLine("Running installer silently...");
                    ProcessStartInfo runPsi = new ProcessStartInfo(installerPath, "/install /quiet /norestart")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process runProc = Process.Start(runPsi);
                    if (runProc != null)
                    {
                        runProc.WaitForExit();
                    }
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

        // .NET 8 is installed — launch the actual app
        ProcessStartInfo psi = new ProcessStartInfo("dotnet", "\"" + dllPath + "\"")
        {
            UseShellExecute = false,
            WorkingDirectory = appDir
        };
        Process proc = Process.Start(psi);
        if (proc != null)
        {
            proc.WaitForExit();
            Environment.Exit(proc.ExitCode);
        }
    }

    static bool IsDotNet8RuntimeInstalled()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("dotnet", "--list-runtimes")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process proc = Process.Start(psi);
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

    static bool IsWingetAvailable()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("where", "winget")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process proc = Process.Start(psi);
            if (proc != null)
            {
                proc.WaitForExit();
                return proc.ExitCode == 0;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}

