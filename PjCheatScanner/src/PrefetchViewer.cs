using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DetectorLite
{
    public static class PrefetchViewer
    {
        public static List<PrefetchEntry> Check()
        {
            var entries = new List<PrefetchEntry>();
            var prefetchDir = @"C:\Windows\Prefetch";
            if (!Directory.Exists(prefetchDir)) return entries;

            foreach (var file in Directory.GetFiles(prefetchDir, "*.pf"))
            {
                try
                {
                    var filename = Path.GetFileNameWithoutExtension(file);
                    var exeName = filename.Split('-')[0];
                    var reason = "Suspicious executable";
                    entries.Add(new PrefetchEntry
                    {
                        ExecutableName = exeName,
                        Reason = reason
                    });
                }
                catch { }
            }
            return entries;
        }
    }

    public class PrefetchEntry
    {
        public string ExecutableName { get; set; }
        public string Reason { get; set; }
    }
}

