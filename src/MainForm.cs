// MainForm.cs - Windows Forms GUI for PjCheatScanner Lite
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DetectorLite
{
    public class MainForm : Form
    {
        private ComboBox processComboBox;
        private CheckBox memoryScannerCheckBox;
        private CheckBox fileViewerCheckBox;
        private Button scanButton;
        private Button refreshButton;
        private Button exportButton;
        private TabControl resultsTabControl;
        private TabPage memoryResultsTab;
        private TabPage fileResultsTab;
        private TabPage summaryTab;
        private ListView memoryListView;
        private ListView fileListView;
        private RichTextBox summaryTextBox;
        private ProgressBar progressBar;
        private Label statusLabel;
        
        private DetectionResults currentResults;

        public MainForm()
        {
            InitializeComponents();
            RefreshProcesses();
        }

        private void InitializeComponents()
        {
            this.Text = "PjCheatScanner Lite";
            this.Size = new System.Drawing.Size(900, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // Process Selection
            Label processLabel = new Label
            {
                Text = "Process Selection:",
                Location = new System.Drawing.Point(20, 20),
                AutoSize = true
            };
            this.Controls.Add(processLabel);

            processComboBox = new ComboBox
            {
                Location = new System.Drawing.Point(20, 45),
                Size = new System.Drawing.Size(400, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            this.Controls.Add(processComboBox);

            refreshButton = new Button
            {
                Text = "Refresh",
                Location = new System.Drawing.Point(430, 45),
                Size = new System.Drawing.Size(100, 25)
            };
            refreshButton.Click += RefreshProcesses;
            this.Controls.Add(refreshButton);

            // Tool Selection
            Label toolsLabel = new Label
            {
                Text = "Tools to Run:",
                Location = new System.Drawing.Point(20, 80),
                AutoSize = true
            };
            this.Controls.Add(toolsLabel);

            memoryScannerCheckBox = new CheckBox
            {
                Text = "Memory Scanner",
                Checked = true,
                Location = new System.Drawing.Point(20, 105),
                AutoSize = true
            };
            this.Controls.Add(memoryScannerCheckBox);

            fileViewerCheckBox = new CheckBox
            {
                Text = "File Viewer (Prefetch/Recent/Injected DLLs)",
                Checked = true,
                Location = new System.Drawing.Point(20, 130),
                AutoSize = true
            };
            this.Controls.Add(fileViewerCheckBox);

            // Scan Button
            scanButton = new Button
            {
                Text = "Start Scan",
                Location = new System.Drawing.Point(20, 165),
                Size = new System.Drawing.Size(150, 35),
                BackColor = System.Drawing.Color.FromArgb(0, 120, 215),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };
            scanButton.Click += StartScan;
            this.Controls.Add(scanButton);

            // Progress Bar
            progressBar = new ProgressBar
            {
                Location = new System.Drawing.Point(20, 210),
                Size = new System.Drawing.Size(840, 23)
            };
            this.Controls.Add(progressBar);

            // Status Label
            statusLabel = new Label
            {
                Text = "Ready",
                Location = new System.Drawing.Point(20, 240),
                AutoSize = true
            };
            this.Controls.Add(statusLabel);

            // Tab Control
            resultsTabControl = new TabControl
            {
                Location = new System.Drawing.Point(20, 270),
                Size = new System.Drawing.Size(840, 350)
            };
            this.Controls.Add(resultsTabControl);

            // Memory Results Tab
            memoryResultsTab = new TabPage("Memory Results");
            memoryListView = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Dock = DockStyle.Fill
            };
            memoryListView.Columns.Add("Process ID", 80);
            memoryListView.Columns.Add("Cheat String", 150);
            memoryListView.Columns.Add("Context", 400);
            memoryListView.Columns.Add("Confidence", 80);
            memoryResultsTab.Controls.Add(memoryListView);
            resultsTabControl.TabPages.Add(memoryResultsTab);

            // File Results Tab
            fileResultsTab = new TabPage("File Results");
            fileListView = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Dock = DockStyle.Fill
            };
            fileListView.Columns.Add("File Path", 300);
            fileListView.Columns.Add("Source", 100);
            fileListView.Columns.Add("Last Seen", 120);
            fileListView.Columns.Add("Reason", 200);
            fileListView.Columns.Add("Confidence", 80);
            fileResultsTab.Controls.Add(fileListView);
            resultsTabControl.TabPages.Add(fileResultsTab);

            // Summary Tab
            summaryTab = new TabPage("Summary");
            summaryTextBox = new RichTextBox
            {
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Consolas", 10)
            };
            summaryTab.Controls.Add(summaryTextBox);
            resultsTabControl.TabPages.Add(summaryTab);

            // Export Button
            exportButton = new Button
            {
                Text = "Export Report",
                Location = new System.Drawing.Point(760, 625),
                Size = new System.Drawing.Size(100, 30)
            };
            exportButton.Click += ExportReport;
            this.Controls.Add(exportButton);
        }

        private void RefreshProcesses()
        {
            var targets = new[] { "javaw", "java", "minecraft" };
            var processes = targets.SelectMany(Process.GetProcessesByName).ToList();

            processComboBox.Items.Clear();
            foreach (var proc in processes)
            {
                bool is64Bit = IsProcess64Bit(proc);
                processComboBox.Items.Add(new ProcessItem
                {
                    Process = proc,
                    DisplayText = $"{proc.ProcessName}.exe (PID {proc.Id}) - {(is64Bit ? "64-bit" : "32-bit")}"
                });
            }
            processComboBox.DisplayMember = "DisplayText";
            processComboBox.ValueMember = "Process";

            if (processComboBox.Items.Count > 0)
                processComboBox.SelectedIndex = 0;
        }

        private bool IsProcess64Bit(Process process)
        {
            if (Environment.Is64BitProcess)
            {
                try
                {
                    if (MemoryScannerLite.IsWow64Process != null)
                    {
                        // This would need the P/Invoke method to be public
                        // For now, assume 64-bit on 64-bit OS
                        return true;
                    }
                }
                catch { }
                return true;
            }
            return false;
        }

        private async void StartScan()
        {
            if (processComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a process to scan.", "No Process Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            scanButton.Enabled = false;
            refreshButton.Enabled = false;
            progressBar.Value = 0;
            statusLabel.Text = "Scanning...";
            
            // Clear previous results
            memoryListView.Items.Clear();
            fileListView.Items.Clear();
            summaryTextBox.Clear();

            var selectedProcess = (ProcessItem)processComboBox.SelectedItem;
            currentResults = new DetectionResults();

            try
            {
                // Memory Scanner
                if (memoryScannerCheckBox.Checked)
                {
                    statusLabel.Text = "Scanning memory...";
                    progressBar.Value = 30;
                    
                    await Task.Run(() =>
                    {
                        currentResults.ScannedProcesses.Add(selectedProcess.Process.Id);
                        var hits = MemoryScannerLite.ScanProcess(selectedProcess.Process);
                        if (hits.Count > 0)
                        {
                            currentResults.MemoryHits[selectedProcess.Process.Id] = hits;
                        }
                    });
                    
                    DisplayMemoryResults();
                }

                // File Viewer
                if (fileViewerCheckBox.Checked)
                {
                    statusLabel.Text = "Scanning files...";
                    progressBar.Value = 60;
                    
                    await Task.Run(() =>
                    {
                        currentResults.FileViewerEntries = FileViewer.Check();
                    });
                    
                    DisplayFileResults();
                }

                // Summary
                progressBar.Value = 100;
                statusLabel.Text = "Scan complete";
                DisplaySummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during scan: {ex.Message}", "Scan Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Scan failed";
            }
            finally
            {
                scanButton.Enabled = true;
                refreshButton.Enabled = true;
            }
        }

        private void DisplayMemoryResults()
        {
            memoryListView.Items.Clear();
            foreach (var kvp in currentResults.MemoryHits)
            {
                foreach (var hit in kvp.Value)
                {
                    var item = new ListViewItem(kvp.Key.ToString());
                    // Parse the hit string to extract cheat string and context
                    var parts = hit.Split(new[] { "Found '", " context: '" }, StringSplitOptions.None);
                    if (parts.Length >= 3)
                    {
                        item.SubItems.Add(parts[1]);
                        item.SubItems.Add(parts[2].TrimEnd('\''));
                    }
                    else
                    {
                        item.SubItems.Add(hit);
                        item.SubItems.Add("");
                    }
                    item.SubItems.Add("High");
                    memoryListView.Items.Add(item);
                }
            }
        }

        private void DisplayFileResults()
        {
            fileListView.Items.Clear();
            foreach (var entry in currentResults.FileViewerEntries)
            {
                var item = new ListViewItem(entry.FilePath);
                item.SubItems.Add(entry.Source);
                item.SubItems.Add(entry.LastSeen.ToString());
                item.SubItems.Add(entry.Reason);
                item.SubItems.Add(entry.Confidence.ToString());
                fileListView.Items.Add(item);
            }
        }

        private void DisplaySummary()
        {
            summaryTextBox.Clear();
            summaryTextBox.AppendText("=== SCAN SUMMARY ===\n\n");
            summaryTextBox.AppendText($"Processes scanned: {currentResults.ScannedProcesses.Count}\n");
            summaryTextBox.AppendText($"Memory hits: {currentResults.MemoryHits.Values.Sum(v => v.Count)}\n");
            summaryTextBox.AppendText($"Suspicious files: {currentResults.FileViewerEntries.Count}\n\n");

            bool anyHits = currentResults.MemoryHits.Any() || currentResults.FileViewerEntries.Any();

            if (anyHits)
            {
                summaryTextBox.SelectionColor = System.Drawing.Color.Red;
                summaryTextBox.AppendText("RESULT: POTENTIAL CHEAT DETECTED\n");
                summaryTextBox.SelectionColor = System.Drawing.Color.Black;
            }
            else
            {
                summaryTextBox.SelectionColor = System.Drawing.Color.Green;
                summaryTextBox.AppendText("RESULT: Clean\n");
                summaryTextBox.SelectionColor = System.Drawing.Color.Black;
            }
        }

        private void ExportReport()
        {
            if (currentResults == null)
            {
                MessageBox.Show("No scan results to export.", "No Results", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "JSON Files|*.json|CSV Files|*.csv",
                DefaultExt = "json",
                FileName = "pjcheatscanner_lite_report.json"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (saveDialog.FilterIndex == 1) // JSON
                    {
                        string json = JsonSerializer.Serialize(currentResults, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(saveDialog.FileName, json);
                    }
                    else // CSV
                    {
                        ExportToCsv(saveDialog.FileName);
                    }
                    MessageBox.Show("Report exported successfully.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export report: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportToCsv(string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                // Memory hits
                writer.WriteLine("=== MEMORY HITS ===");
                writer.WriteLine("Process ID,Cheat String,Context");
                foreach (var kvp in currentResults.MemoryHits)
                {
                    foreach (var hit in kvp.Value)
                    {
                        var parts = hit.Split(new[] { "Found '", " context: '" }, StringSplitOptions.None);
                        var cheatString = parts.Length >= 2 ? parts[1] : hit;
                        var context = parts.Length >= 3 ? parts[2].TrimEnd('\'') : "";
                        writer.WriteLine($"{kvp.Key},\"{cheatString}\",\"{context}\"");
                    }
                }

                // File entries
                writer.WriteLine("\n=== FILE ENTRIES ===");
                writer.WriteLine("File Path,Source,Last Seen,Reason,Confidence");
                foreach (var entry in currentResults.FileViewerEntries)
                {
                    writer.WriteLine($"\"{entry.FilePath}\",{entry.Source},{entry.LastSeen},\"{entry.Reason}\",{entry.Confidence}");
                }
            }
        }

        private class ProcessItem
        {
            public Process Process { get; set; }
            public string DisplayText { get; set; }
        }
    }
}
