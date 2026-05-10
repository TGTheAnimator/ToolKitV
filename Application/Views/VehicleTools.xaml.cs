using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        }

        private void SetBusy(bool busy)
        {
            ScanButton.IsButtonEnabled  = !busy && CanProceed();
            MergeButton.IsButtonEnabled = !busy && CanProceed();
        }

        // ── Scan (dry run — discovery only, no writes) ────────────────────────

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            ScanButton.Title = "Scanning...";
            ResetResultsDisplay();

            MergeResults counts = await Task.Run(() => ScanOnly(_resourcePath));

            Dispatcher.Invoke(() =>
            {
                ScanVehiclesCount.Text   = counts.VehiclesFilesFound.ToString();
                ScanHandlingCount.Text   = counts.HandlingFilesFound.ToString();
                ScanCarcolsCount.Text    = counts.CarcolsFilesFound.ToString();
                ScanVariationsCount.Text = counts.VariationsFilesFound.ToString();
                ScanLayoutsCount.Text    = counts.LayoutsFilesFound.ToString();
                ResultStatus.Text        = "Ready to merge";
            });

            ScanButton.Title = "Scan";
            SetBusy(false);
        }

        // ── Merge (full operation) ────────────────────────────────────────────

        // Delegate types matching VehicleMetaMerger signature
        public delegate void MergeProgress(MergeResults data, int progress);
        private Delegate? _mergeProgressHandler;

        public MergeProgress MergeProgressCallback => MergeProgressValue;

        private void MergeProgressValue(MergeResults data, int progress)
        {
            Dispatcher.Invoke(() =>
            {
                MergeButton.SetProgress(progress);
                ResultVehicles.Text   = data.VehiclesMerged   > 0 ? data.VehiclesMerged.ToString()   : "—";
                ResultHandling.Text   = data.HandlingMerged   > 0 ? data.HandlingMerged.ToString()   : "—";
                ResultKitsLights.Text = (data.KitsMerged + data.LightsMerged) > 0
                    ? $"{data.KitsMerged} / {data.LightsMerged}" : "—";
                ResultVariations.Text   = data.VariationsMerged   > 0 ? data.VariationsMerged.ToString()   : "—";
                ResultConflicts.Text    = data.ConflictsResolved > 0 ? data.ConflictsResolved.ToString() : "0";
                ResultDupes.Text        = data.DuplicatesSkipped > 0 ? data.DuplicatesSkipped.ToString() : "0";
            });
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

            SetBusy(true);
            MergeButton.Title = "Merging...";
            ResultStatus.Text = "Running...";
            ResetResultsDisplay();

            _mergeProgressHandler = MergeProgressCallback;

            MergeResults result = await Task.Run(() =>
                MergeVehicleMetas(_resourcePath, _backupPath, _cleanupSources, _mergeProgressHandler));

            Dispatcher.Invoke(() =>
            {
                // Final counts
                ScanVehiclesCount.Text   = result.VehiclesFilesFound.ToString();
                ScanHandlingCount.Text   = result.HandlingFilesFound.ToString();
                ScanCarcolsCount.Text    = result.CarcolsFilesFound.ToString();
                ScanVariationsCount.Text = result.VariationsFilesFound.ToString();
                ScanLayoutsCount.Text    = result.LayoutsFilesFound.ToString();

                ResultVehicles.Text     = result.VehiclesMerged.ToString();
                ResultHandling.Text     = result.HandlingMerged.ToString();
                ResultKitsLights.Text   = $"{result.KitsMerged} / {result.LightsMerged}";
                ResultVariations.Text   = result.VariationsMerged.ToString();
                ResultConflicts.Text    = result.ConflictsResolved.ToString();
                ResultDupes.Text        = result.DuplicatesSkipped.ToString();
                ResultStatus.Text       = "✓ Done";

                if (result.Warnings?.Count > 0)
                    ResultConflicts.Foreground = System.Windows.Media.Brushes.OrangeRed;
            });

            MergeButton.Title = "Merge";
            MergeButton.ResetProgress();
            SetBusy(false);
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

        // ── UI helpers ────────────────────────────────────────────────────────

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
