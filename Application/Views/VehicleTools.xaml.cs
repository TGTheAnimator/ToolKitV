using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ToolKitV.Models;
using static ToolKitV.Models.VehicleMetaMerger;

namespace ToolKitV.Views
{
    public partial class VehicleTools : UserControl
    {
        // ── State ─────────────────────────────────────────────────────────────

        private string _resourcePath = string.Empty;
        private string _backupPath   = string.Empty;
        private bool   _cleanupSources;

        // ── Constructor ───────────────────────────────────────────────────────

        public VehicleTools()
        {
            InitializeComponent();
        }

        // ── Folder bindings ───────────────────────────────────────────────────

        private void OnResourcePathChanged(object sender, PropertyChangedEventArgs e)
        {
            _resourcePath = ResourceFolder.Path;
            RefreshButtonState();
        }

        private void UIElement_OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && Directory.Exists(files[0]))
                {
                    ResourceFolder.Path = files[0];
                }
            }
        }

        private void OnBackupPathChanged(object sender, PropertyChangedEventArgs e)
        {
            _backupPath = BackupFolder.Path;
        }

        private void CleanupSources_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            _cleanupSources = CleanupSources.IsToogled;
        }

        // ── Button state ──────────────────────────────────────────────────────

        private bool CanProceed() =>
            !string.IsNullOrWhiteSpace(_resourcePath) && Directory.Exists(_resourcePath);

        private void RefreshButtonState()
        {
            bool ok = CanProceed();
            ScanButton.IsButtonEnabled  = ok;
            MergeButton.IsButtonEnabled = ok;
            ScanModelsButton.IsButtonEnabled = ok;
        }

        private void SetBusy(bool busy)
        {
            ScanButton.IsButtonEnabled  = !busy && CanProceed();
            MergeButton.IsButtonEnabled = !busy && CanProceed();
            ScanModelsButton.IsButtonEnabled = !busy && CanProceed();
        }

        // ── Scan (dry run — discovery only, no writes) ────────────────────────

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            ScanButton.Title = "Scanning...";
            ResetResultsDisplay();

            MergeResults counts = await Task.Run(() => ScanOnly(_resourcePath));

            ScanVehiclesCount.Text   = counts.VehiclesFilesFound.ToString();
            ScanHandlingCount.Text   = counts.HandlingFilesFound.ToString();
            ScanCarcolsCount.Text    = counts.CarcolsFilesFound.ToString();
            ScanVariationsCount.Text = counts.VariationsFilesFound.ToString();
            ScanLayoutsCount.Text    = counts.LayoutsFilesFound.ToString();
            ResultStatus.Text        = "Ready to merge";

            ScanButton.Title = "Scan";
            SetBusy(false);
        }

        // ── Merge (full operation) ────────────────────────────────────────────

        private void MergeProgressValue((MergeResults data, int progress) report)
        {
            var data = report.data;
            var progress = report.progress;
            
            MergeButton.SetProgress(progress);
            ResultVehicles.Text   = data.VehiclesMerged   > 0 ? data.VehiclesMerged.ToString()   : "—";
            ResultHandling.Text   = data.HandlingMerged   > 0 ? data.HandlingMerged.ToString()   : "—";
            ResultKitsLights.Text = (data.KitsMerged + data.LightsMerged + data.SirenSettingsMerged) > 0
                ? $"{data.KitsMerged} / {data.LightsMerged} / {data.SirenSettingsMerged}" : "—";
            ResultVariations.Text   = data.VariationsMerged   > 0 ? data.VariationsMerged.ToString()   : "—";
            ResultConflicts.Text    = data.ConflictsResolved > 0 ? data.ConflictsResolved.ToString() : "0";
            ResultDupes.Text        = data.DuplicatesSkipped > 0 ? data.DuplicatesSkipped.ToString() : "0";
        }

        private async void MergeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CanProceed()) return;

            // Warn if no backup folder is set
            if (string.IsNullOrWhiteSpace(_backupPath))
            {
                var confirm = MessageBox.Show(
                    "No backup folder is set. If the merge is incorrect, you won't be able to recover the originals.\n\nContinue without a backup?",
                    "TGToolKit — No Backup Folder",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes) return;
            }

            // 1. Lock the UI to prevent double-execution
            SetBusy(true);
            MergeButton.Title = "Merging...";
            ResultStatus.Text = "Running...";
            ResetResultsDisplay();

            try
            {
                // 2. Gather UI values safely
                string resourcePath = _resourcePath;
                string backupPath = _backupPath;
                bool cleanupSources = _cleanupSources;

                // 3. Initialize the Async Logger
                await using var logWriter = new LogWriter("=== Vehicle Meta Merge started via UI ===");

                // 4. Offload the heavy lifting to the ThreadPool
                var progress = new Progress<(MergeResults, int)>(MergeProgressValue);
                MergeResults result = await Task.Run(() => MergeVehicleMetas(resourcePath, backupPath, cleanupSources, progress, logWriter));

                // 5. Handle Success
                // Final counts
                ScanVehiclesCount.Text   = result.VehiclesFilesFound.ToString();
                ScanHandlingCount.Text   = result.HandlingFilesFound.ToString();
                ScanCarcolsCount.Text    = result.CarcolsFilesFound.ToString();
                ScanVariationsCount.Text = result.VariationsFilesFound.ToString();
                ScanLayoutsCount.Text    = result.LayoutsFilesFound.ToString();

                ResultVehicles.Text     = result.VehiclesMerged.ToString();
                ResultHandling.Text     = result.HandlingMerged.ToString();
                ResultKitsLights.Text   = $"{result.KitsMerged} / {result.LightsMerged} / {result.SirenSettingsMerged}";
                ResultVariations.Text   = result.VariationsMerged.ToString();
                ResultConflicts.Text    = result.ConflictsResolved.ToString();
                ResultDupes.Text        = result.DuplicatesSkipped.ToString();
                ResultStatus.Text       = "✓ Done";

                if (result.Warnings?.Count > 0)
                    ResultConflicts.Foreground = System.Windows.Media.Brushes.OrangeRed;

                MessageBox.Show(
                    "Vehicle metadata consolidation complete.",
                    "TGToolKit — Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"A fatal error occurred during vehicle meta merge:\n\n{ex.Message}",
                    "TGToolKit — Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                ResultStatus.Text = "Error";
            }
            finally
            {
                // 6. Always unlock the UI
                MergeButton.Title = "Merge";
                MergeButton.ResetProgress();
                SetBusy(false);
            }
        }

        // ── Scan-only helper (discovery pass, no IO writes) ───────────────────

        private static MergeResults ScanOnly(string resourceDirectory)
        {
            string[] metaNames = { "vehicles.meta", "handling.meta", "carcols.meta", "carvariations.meta", "vehiclelayouts.meta" };
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in metaNames) counts[name] = 0;

            foreach (string file in Directory.EnumerateFiles(resourceDirectory, "*.meta", SearchOption.AllDirectories))
            {
                string fname = Path.GetFileName(file).ToLowerInvariant();
                if (counts.ContainsKey(fname)) counts[fname]++;
            }

            return new MergeResults
            {
                VehiclesFilesFound   = counts["vehicles.meta"],
                HandlingFilesFound   = counts["handling.meta"],
                CarcolsFilesFound    = counts["carcols.meta"],
                VariationsFilesFound = counts["carvariations.meta"],
                LayoutsFilesFound    = counts["vehiclelayouts.meta"],
                Warnings             = new List<string>(),
            };
        }

        // ── Scan YFT Models ───────────────────────────────────────────────────

        private async void ScanModelsButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_resourcePath) || !Directory.Exists(_resourcePath)) return;

            // 1. Lock the UI
            SetBusy(true);
            ScanModelsButton.Title = "Scanning Models...";
            ResetModelScanDisplay();

            try
            {
                // 2. Gather UI values
                string path = _resourcePath;

                // 3. Initialize Logger
                await using var logWriter = new LogWriter("=== Legacy Model Scan started via UI ===");

                // 4. Offload heavy lifting
                var progress = new Progress<int>(percent => 
                {
                    ScanModelsButton.SetProgress(percent);
                    ScanModelsButton.Title = $"Scanning... {percent}%";
                });

                ModelScanner.ScanResults results = await Task.Run(() => 
                    ModelScanner.ScanDirectoryAsync(path, progress, logWriter));

                // 5. Handle Success
                if (results != null)
                {
                    ModelScanTotal.Text = results.TotalFilesScanned.ToString();
                    ModelScanSafe.Text = results.SafeFiles.ToString();
                    ModelScanWarnings.Text = results.WarningFiles.ToString();
                    ModelScanCritical.Text = results.CriticalFiles.ToString();

                    if (results.CriticalFiles > 0)
                    {
                        MessageBox.Show(
                            $"Found {results.CriticalFiles} critical model(s) exceeding GTA V engine limits! These will cause the 'georgia-alaska-october' crash.\n\nCheck 'oversized_models_report.txt' in the selected folder for details.",
                            "TGToolKit — Critical Models Found",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                    else if (results.WarningFiles > 0)
                    {
                        MessageBox.Show(
                            $"Found {results.WarningFiles} heavy model(s). They might not crash the game but can cause instability or FPS drops.\n\nCheck 'oversized_models_report.txt' in the selected folder for details.",
                            "TGToolKit — Heavy Models Found",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                    else if (results.TotalFilesScanned > 0)
                    {
                        MessageBox.Show(
                            $"All {results.TotalFilesScanned} scanned models are within safe limits. Your server shouldn't crash from these geometries.",
                            "TGToolKit — Scan Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"A fatal error occurred during model analysis:\n\n{ex.Message}", "TGToolKit — Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 6. Always unlock
                SetBusy(false);
                ScanModelsButton.Title = "Scan YFT Models";
                ScanModelsButton.ResetProgress();
            }
        }

        // ── UI helpers ────────────────────────────────────────────────────────

        private void ResetModelScanDisplay()
        {
            ModelScanTotal.Text    = "—";
            ModelScanSafe.Text     = "—";
            ModelScanWarnings.Text = "—";
            ModelScanCritical.Text = "—";
        }

        private void ResetResultsDisplay()
        {
            ResultVehicles.Text   = "—";
            ResultHandling.Text   = "—";
            ResultKitsLights.Text = "—";
            ResultVariations.Text = "—";
            ResultConflicts.Text  = "—";
            ResultDupes.Text      = "—";
            ResultStatus.Text     = "—";
            ResultConflicts.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0xFF, 0xFF, 0x55, 0x55));
        }
    }
}
