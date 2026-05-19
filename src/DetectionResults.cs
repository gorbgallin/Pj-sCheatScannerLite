// DetectionResults.cs - Results model for scan output
using System.Collections.Generic;

namespace DetectorLite
{
    public class DetectionResults
    {
        public List<int> ScannedProcesses { get; set; } = new();
        public Dictionary<int, List<string>> MemoryHits { get; set; } = new();
        public List<FileViewerEntry> FileViewerEntries { get; set; } = new();
    }
}
