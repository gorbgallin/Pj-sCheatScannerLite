// MemoryScannerLite.cs - Lightweight process memory scanner for cheat strings
// Uses Windows P/Invoke ReadProcessMemory / VirtualQueryEx
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DetectorLite
{
    public static class MemoryScannerLite
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private const int PROCESS_VM_READ = 0x0010;
        private const int PROCESS_QUERY_INFORMATION = 0x0400;
        private const int MEM_COMMIT = 0x1000;
        private const uint PAGE_READONLY = 0x02;
        private const uint PAGE_READWRITE = 0x04;
        private const uint PAGE_WRITECOPY = 0x08;
        private const uint PAGE_EXECUTE_READ = 0x20;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint PAGE_EXECUTE_WRITECOPY = 0x80;
        private const uint PAGE_GUARD = 0x100;

        private const int CHUNK_SIZE = 256 * 1024; // 256KB chunks
        private const int OVERLAP = 512;
        private const int MIN_STRING_LEN = 4;
        private const int MAX_STRING_LEN = 128;
        private const int CONTEXT_LEN = 40;
        private const int TIMEOUT_SECONDS = 30;

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        private static readonly HashSet<string> _cheats = LoadCheatStrings();

        private static HashSet<string> LoadCheatStrings()
        {
            var cheats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                            cheats.Add(trimmed);
                    }
                    if (cheats.Count > 0) break;
                }
                catch { }
            }

            if (cheats.Count == 0)
            {
                // Full embedded fallback — no external file needed
                string[] embedded = {
                    "triggerbot", "killaura", "selfdestruct", "strafe", "aimassist",
                    "pingspoof", "meteorclient", "virgin", "prestige", "s3lfd3struct",
                    "playerreach", "jumpreset", "fakelag", "autoclick", "doomsdayclient.com",
                    "hotbarswap", "switchdelay", "switch_delay_ms", "swapBackToOriginalSlot",
                    "attackRegisteredThisClick", "findKnockbackSword", "lvstrng", "safe anchor",
                    "safe_anchor", "auto crystal", "anchor macro", "auto totem", "autototem",
                    "auto_hit_crystal", "auto_inventory_totem", "EndCrystalItemMixin",
                    "isDeadBodyNearby", "POT_CHEATS", "getBlockBreakingCooldown",
                    "Anch0r Macr0", "Aut0H1tCryst4l", "L3g1t R3t0t3m", "Anchor_Macro",
                    "Auto_crystal", "hookCancelBlockBreaking", "canPlaceCrystalServer",
                    "onSwapLastAttackedTicksReset", "redirectSelectedSlot", "getHandSwingDuration",
                    "onBeginRenderTick", "PlayerMoveC2SPacketAccessor", "clickSimulation",
                    "switchDelay", "switchChance", "placeChance", "glowstoneDelay",
                    "glowstoneChance", "explodeDelay", "explodeChance", "explodeSlot",
                    "antiWeakness", "damageTick", "breakChance", "breakDelay",
                    "stopOnCrystal", "processCrystal", "swapToWeapon", "isObsidianOrBedrock",
                    "isValidCrystalPosition", "processAnchorPvP", "isValidAnchorPosition",
                    "AutoCrystal", "autocrystal", "auto crystal", "AutoHitCrystal",
                    "autohitcrystal", "dontPlaceCrystal", "dontBreakCrystal",
                    "canPlaceCrystalServer", "autoCrystalPlaceClock", "AutoAnchor",
                    "autoanchor", "auto anchor", "DoubleAnchor", "safe anchor",
                    "safeanchor", "anchortweaks", "anchor macro", "AutoTotem",
                    "autototem", "auto totem", "InventoryTotem", "inventorytotem",
                    "HoverTotem", "hover totem", "legittotem", "AutoPot", "autopot",
                    "auto pot", "speedPotSlot", "strengthPotSlot", "AutoArmor",
                    "autoarmor", "auto armor", "preventSwordBlockBreaking",
                    "preventSwordBlockAttack", "AutoDoubleHand", "autodoublehand",
                    "auto double hand", "AutoClicker", "AimAssist", "aimassist",
                    "aim assist", "trigger bot", "shieldbreaker", "shield breaker",
                    "axespam", "axe spam", "FakeLag", "ping spoof",
                    "FakeInv", "pushOutOfBlocks", "onPushOutOfBlocks",
                    "webmacro", "web macro", "JumpReset", "Donut",
                    "setBlockBreakingCooldown", "setItemUseCooldown", "onBlockBreaking",
                    "invokeDoAttack", "invokeDoItemUse", "setSelectedSlot", "getSelectedSlot",
                    "blockBreakingCooldown", "invokeOnMouseButton",
                    "onSwapLastAttackedTicksReset", "getVisualAttackCooldownProgressPerTick",
                    "getHandSwingDuration", "onBeginRenderTick", "PlayerMoveC2SPacketAccessor",
                    "redirectSelectedSlot", "hookCancelBlockBreaking", "EndCrystalItemMixin",
                    "endcrystalitemmixin", "WalksyCrystalOptimizerMod", "arrayOfString",
                    "dqrkis", "StringObfuscator", "POT_CHEATS", "onShouldRenderBlockOutline",
                    "predictCrystals", "noOffhandTotem", "getNearByCrystals", "slotExplode",
                    "needToPlaceRails", "findTotemSlot", "activateOnRightClick",
                    "crystalPlaceClock", "isDeadBodyNearby", "CrystalTwiceClock",
                    "mainHandStack", "attackInAir", "attackOnJump", "onDestruct",
                    "getGlowstoneChance", "isAutoCharge", "getPlaceChance", "getSwitchDelay",
                    "getGlowstoneDelay", "getExplodeDelay", "getExplodeSlotIndex",
                    "getPlaceDelayTicks", "getBreakDelayTicks", "getBreakChance",
                    "isSpawnersEnabled", "isShulkersEnabled", "onModuleDisabled",
                    "switchToBestTool", "switchToBestWeapon", "isLootProtect",
                    "getMinHunger", "isTracersEnabled", "getSelectedBlocks",
                    "isChestsEnabled", "inventoryToMenuSlot", "throwPearl",
                    "isLeftHoldOnly",
                    "Automatically switches to sword when hitting with totem",
                    "Failed to switch to mace after axe!",
                    "Breaking shield with axe...", "TrilliumSolutions", "self destruct",
                    "ＡｕｔｏＣｒｙｓｔａｌ", "Ａｕｔｏ Ｃｒｙｓｔａｌ", "ＡｕｔｏＨｉｔＣｒｙｓｔａｌ",
                    "Ａ．ｕｔｏ Ｃｒｙｓｔａｌ", "Ａ．ｕｔｏＣｒｙｓｔａｌＬＶ２", "Ａ．ｕｔｏ Ｈｉｔ Ｃｒｙｓｔａｌ",
                    "ＡｕｔｏＡｎｃｈｏｒ", "Ａｕｔｏ Ａｎｃｈｏｒ", "ＤｏｕｂｌｅＡｎｃｈｏｒ",
                    "Ｄｏｕｂｌｅ Ａｎｃｈｏｒ", "ＳａｆｅＡｎｃｈｏｒ", "Ｓａｆｅ Ａｎｃｈｏｒ",
                    "Ａｎｃｈｏｒ Ｍａｃｒｏ", "Ａ．ｎｃｈｏｒ Ｍａｃｒｏ", "Ａ．ｎｃｈｏｒ Ｍａｃｒｏ Ｖ２",
                    "Ｄ．ｏｕｂｌｅ Ａｎｃｈｏｒ", "Ｓ．ａｆｅＡｎｃｈｏｒ", "ＡｕｔｏＴｏｔｅｍ",
                    "Ａｕｔｏ Ｔｏｔｅｍ", "Ａｕｔｏ Ｔｏｔｅｍ Ｈｉｔ", "Ａ．ｕｔｏ Ｔｏｔｅｍ Ｈｉｔ",
                    "ＨｏｖｅｒＴｏｔｅｍ", "Ｈｏｖｅｒ Ｔｏｔｅｍ", "ＩｎｖｅｎｔｏｒｙＴｏｔｅｍ",
                    "Ｈ．ｏｖｅｒ Ｔｏｔｅｍ", "Ａ．ｕｔｏ Ｉｎｖｅｎｔｏｒｙ Ｔｏｔｅｍ",
                    "Ｆ．ｏｒｃｅ Ｔｏｔｅｍ", "Ｔ．ｏｔｅｍ Ｆｉｒｓｔ", "Ｔ．ｏｔｅｍ Ｏｆｆｈａｎｄ",
                    "Ｔ．ｏｔｅｍ Ｓｌｏｔ", "Ｈ．ｏｖｅｒ", "Ｗ．ｏｒｋ Ｗｉｔｈ Ｔｏｔｅｍ",
                    "ＡｕｔｏＤｏｕｂｌｅＨａｎｄ", "Ａｕｔｏ Ｄｏｕｂｌｅ Ｈａｎｄ",
                    "Ａ．ｕｔｏ Ｄｏｕｂｌｅ Ｈａｎｄ", "Ａ．ｃｔｉｖａｔｅ Ｋｅｙ",
                    "Ｗ．ｈｉｌｅ Ｕｓｅ", "Ｓ．ｔｏｐ ｏｎ Ｋｉｌｌ",
                    "Ｃ．ｌｉｃｋ Ｓｉｍｕｌａｔｉｏｎ", "Ｓ．ｗｉｔｃｈ Ｄｅｌａｙ",
                    "Ｓ．ｗｔｃｈ Ｃｈａｎｃｅ", "Ｐ．ｌａｃｅ Ｃｈａｎｃｅ",
                    "Ｇ．ｌｏｗｓｔｏｎｅ Ｄｅｌａｙ", "Ｇ．ｌｏｗｓｔｏｎｅ Ｃｈａｎｃｅ",
                    "Ｅ．ｘｐｌｏｄｅ Ｄｅｌａｙ", "Ｅ．ｘｐｌｏｄｅ Ｃｈａｎｃｅ",
                    "Ｅ．ｘｐｌｏｄｅ Ｓｌｏｔ", "Ｏ．ｎｌｙ Ｏｗｎ",
                    "Ｏ．ｎｌｙ Ｃｈａｒｇｅ", "Ｒ．ａｎｄｏｍ Ｇｌｏｗｓｔｏｎｅ"
                };
                foreach (var s in embedded) cheats.Add(s);
            }
            return cheats;
        }

        public static List<string> ScanProcess(Process process)
        {
            var hits = new List<string>();
            var earlyStopHits = new HashSet<string>();
            Console.WriteLine($"  Scanning PID {process.Id} ({process.ProcessName})...");

            IntPtr hProcess = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, process.Id);
            if (hProcess == IntPtr.Zero)
            {
                Console.WriteLine("  Access denied - run as Administrator");
                return hits;
            }

            var sw = Stopwatch.StartNew();
            int regionsScanned = 0;
            long totalBytes = 0;

            try
            {
                IntPtr addr = IntPtr.Zero;
                MEMORY_BASIC_INFORMATION mbi;
                uint mbiSize = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

                while (VirtualQueryEx(hProcess, addr, out mbi, mbiSize) > 0)
                {
                    if (sw.Elapsed.TotalSeconds > TIMEOUT_SECONDS)
                    {
                        Console.WriteLine($"  Timeout after {TIMEOUT_SECONDS}s");
                        break;
                    }
                    if (earlyStopHits.Count >= 1000)
                    {
                        Console.WriteLine("  Stopping... 1000+ strings found LOL?");
                        break;
                    }

                    long currentAddr = addr.ToInt64();
                    long regionSize = mbi.RegionSize.ToInt64();
                    long nextAddr = currentAddr + regionSize;

                    if (nextAddr <= currentAddr || nextAddr > 0x7FFFFFFFFFFFFFFF)
                        break;

                    addr = new IntPtr(nextAddr);

                    if ((mbi.State & MEM_COMMIT) == 0)
                        continue;

                    if ((mbi.Protect & PAGE_GUARD) != 0)
                        continue;

                    bool readable = (mbi.Protect & PAGE_READONLY) != 0 ||
                                    (mbi.Protect & PAGE_READWRITE) != 0 ||
                                    (mbi.Protect & PAGE_WRITECOPY) != 0 ||
                                    (mbi.Protect & PAGE_EXECUTE_READ) != 0 ||
                                    (mbi.Protect & PAGE_EXECUTE_READWRITE) != 0 ||
                                    (mbi.Protect & PAGE_EXECUTE_WRITECOPY) != 0;

                    if (!readable)
                        continue;

                    if (regionSize > 64L * 1024 * 1024)
                        continue;

                    regionsScanned++;
                    totalBytes += ScanRegion(hProcess, mbi.BaseAddress, (int)regionSize, hits, earlyStopHits);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
            }
            finally
            {
                CloseHandle(hProcess);
            }

            Console.WriteLine($"  Done: {regionsScanned} regions, {totalBytes / 1024}KB in {sw.ElapsedMilliseconds}ms");

            var uniqueHits = new HashSet<string>();
            var orderedHits = new List<string>();
            foreach (var hit in hits)
            {
                if (uniqueHits.Add(hit))
                    orderedHits.Add(hit);
            }

            foreach (var hit in orderedHits)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    [HIT] {hit}");
                Console.ResetColor();
            }

            return orderedHits;
        }

        private static long ScanRegion(IntPtr hProcess, IntPtr baseAddr, int regionSize, List<string> hits, HashSet<string> earlyStopHits)
        {
            long totalRead = 0;

            if (regionSize <= CHUNK_SIZE)
            {
                byte[] buffer = new byte[regionSize];
                if (ReadProcessMemory(hProcess, baseAddr, buffer, regionSize, out int bytesRead))
                {
                    ExtractStrings(buffer, bytesRead, hits, earlyStopHits);
                    totalRead += bytesRead;
                }
                return totalRead;
            }

            byte[] overlap = new byte[0];
            for (int offset = 0; offset < regionSize; offset += CHUNK_SIZE)
            {
                int bytesToRead = Math.Min(CHUNK_SIZE, regionSize - offset);
                byte[] chunkBuffer = new byte[bytesToRead];

                IntPtr readAddr = new IntPtr(baseAddr.ToInt64() + offset);
                if (!ReadProcessMemory(hProcess, readAddr, chunkBuffer, bytesToRead, out int bytesRead))
                    continue;

                totalRead += bytesRead;

                if (overlap.Length > 0)
                {
                    byte[] combined = new byte[overlap.Length + bytesRead];
                    Buffer.BlockCopy(overlap, 0, combined, 0, overlap.Length);
                    Buffer.BlockCopy(chunkBuffer, 0, combined, overlap.Length, bytesRead);
                    ExtractStrings(combined, combined.Length, hits, earlyStopHits);
                }
                else
                {
                    ExtractStrings(chunkBuffer, bytesRead, hits, earlyStopHits);
                }

                int trailLen = Math.Min(OVERLAP, bytesRead);
                overlap = new byte[trailLen];
                Buffer.BlockCopy(chunkBuffer, bytesRead - trailLen, overlap, 0, trailLen);
            }

            return totalRead;
        }

        private static void ExtractStrings(byte[] buffer, int len, List<string> hits, HashSet<string> earlyStopHits)
        {
            // ASCII extraction
            int start = 0;
            for (int i = 0; i <= len; i++)
            {
                bool printable = i < len && IsPrintable(buffer[i]);
                if (!printable || (i - start) >= MAX_STRING_LEN)
                {
                    if (i - start >= MIN_STRING_LEN)
                    {
                        int length = Math.Min(i - start, MAX_STRING_LEN);
                        string text = Encoding.ASCII.GetString(buffer, start, length).ToLowerInvariant();
                        SearchText(text, hits, earlyStopHits);
                    }
                    start = i + 1;
                }
            }

            // UTF-16LE extraction (Java strings are typically UTF-16)
            if (len < MIN_STRING_LEN * 2) return;

            start = -1;
            for (int i = 0; i + 1 <= len; i += 2)
            {
                bool valid = i + 1 < len && IsPrintable(buffer[i]) && buffer[i + 1] == 0;

                if (!valid || (start >= 0 && (i - start) / 2 >= MAX_STRING_LEN))
                {
                    if (start >= 0 && i - start >= MIN_STRING_LEN * 2)
                    {
                        int charCount = Math.Min((i - start) / 2, MAX_STRING_LEN);
                        char[] chars = new char[charCount];
                        for (int j = 0; j < charCount; j++)
                            chars[j] = (char)buffer[start + j * 2];
                        SearchText(new string(chars).ToLowerInvariant(), hits, earlyStopHits);
                    }
                    start = valid ? i : -1;
                }
                else if (start < 0 && valid)
                {
                    start = i;
                }
            }

            if (start >= 0 && len - start >= MIN_STRING_LEN * 2)
            {
                int charCount = Math.Min((len - start) / 2, MAX_STRING_LEN);
                char[] chars = new char[charCount];
                for (int j = 0; j < charCount; j++)
                    chars[j] = (char)buffer[start + j * 2];
                SearchText(new string(chars).ToLowerInvariant(), hits, earlyStopHits);
            }
        }

        private static bool IsPrintable(byte b) => b >= 32 && b <= 126;

        private static void SearchText(string text, List<string> hits, HashSet<string> earlyStopHits)
        {
            foreach (var cheat in _cheats)
            {
                int idx = text.IndexOf(cheat, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    string context = GetContext(text, idx, cheat.Length);
                    string hit = $"Found '{cheat}' context: '{context}'";
                    hits.Add(hit);
                    earlyStopHits.Add(cheat);
                }
            }
        }

        private static string GetContext(string text, int matchIndex, int matchLength)
        {
            int start = Math.Max(0, matchIndex - CONTEXT_LEN / 2);
            int end = Math.Min(text.Length, matchIndex + matchLength + CONTEXT_LEN / 2);
            return text.Substring(start, end - start);
        }
    }
}

