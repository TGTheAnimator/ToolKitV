using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ToolKitV.Models;

namespace ToolKitV.Views
{
    public partial class AssetAnalyzer : UserControl
    {
        private string _resourcePath = string.Empty;

        public AssetAnalyzer()
        {
            InitializeComponent();
        }

        private void OnResourcePathChanged(object sender, PropertyChangedEventArgs e)
        {
            _resourcePath = ResourceFolder.Path;
            bool ok = !string.IsNullOrWhiteSpace(_resourcePath) && Directory.Exists(_resourcePath);
            RunAuditButton.IsButtonEnabled = ok;
            RunModelScanButton.IsButtonEnabled = ok;
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

        private async void RunAuditButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_resourcePath) || !Directory.Exists(_resourcePath)) return;

            // 1. Lock the UI
            SetButtonsEnabled(false);
            RunAuditButton.Title = "Auditing Resource...";
            ResetDisplay();

            try
            {
                // 2. Gather UI values
                string path = _resourcePath;

                // 3. Initialize Logger
                await using var logWriter = new LogWriter("=== Resource Audit started via UI ===");

                // 4. Offload heavy lifting
                var progress = new Progress<(int progress, int current, int total)>(report => 
                {
                    RunAuditButton.SetProgress(report.progress);
                    RunAuditButton.Title = $"Auditing {report.current}/{report.total}...";
                });

                AuditResult? result = await Task.Run(() => ResourceAudit.AuditResource(path, progress, logWriter));

                // 5. Handle Success
                if (result != null)
                {
                    ResultStatus.Text = result.OverallStatus.ToString();
                    ResultStatus.Foreground = result.OverallStatus == DangerLevel.Critical ? new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x55, 0x55)) :
                                                result.OverallStatus == DangerLevel.Warning ? new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xA5, 0x00)) :
                                                new SolidColorBrush(Color.FromArgb(0xFF, 0x4C, 0xFF, 0x70));

                    ResultTotalMB.Text = $"{result.TotalEstimatedMB:F2} MB";
                    ResultTextureMB.Text = $"{result.TextureVirtualMB:F2} MB";
                    ResultModelMB.Text = $"{result.ModelVirtualMB:F2} MB";
                    ResultCollisionMB.Text = $"{result.CollisionVirtualMB:F2} MB";
                    ResultAudioMB.Text = $"{result.AudioDiskMB:F2} MB";

                    if (result.Recommendations.Count > 0)
                    {
                        RecommendationsList.ItemsSource = result.Recommendations;
                        RecommendationsList.Visibility = Visibility.Visible;
                        NoRecommendationsText.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        RecommendationsList.ItemsSource = null;
                        RecommendationsList.Visibility = Visibility.Collapsed;
                        NoRecommendationsText.Visibility = Visibility.Visible;
                    }

                    // 6. Professional Summary Alert
                    string summaryMessage = $"Audit finished in {result.AuditDuration.TotalSeconds:F1} seconds.\n\n" +
                                            $"Total Estimated Memory: {result.TotalEstimatedMB:F2} MB\n" +
                                            $"Status: {result.OverallStatus}";

                    MessageBoxImage icon = (result.OverallStatus == DangerLevel.Critical) ? MessageBoxImage.Error : 
                                           (result.OverallStatus == DangerLevel.Warning)  ? MessageBoxImage.Warning : 
                                           MessageBoxImage.Information;

                    if (result.OverallStatus != DangerLevel.Safe)
                    {
                        summaryMessage += "\n\nA detailed report ('resource_audit_report.txt') has been saved to the folder.";
                    }

                    MessageBox.Show(summaryMessage, "TGToolKit — Audit Complete", MessageBoxButton.OK, icon);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"A fatal error occurred during resource audit:\n\n{ex.Message}", "TGToolKit — Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 6. Always unlock
                SetButtonsEnabled(true);
                RunAuditButton.Title = "Run Asset Audit";
                RunAuditButton.ResetProgress();
            }
        }

        private async void RunModelScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_resourcePath) || !Directory.Exists(_resourcePath)) return;

            // 1. Lock the UI
            SetButtonsEnabled(false);
            RunModelScanButton.Title = "Scanning Models...";
            ResetDisplay();

            try
            {
                // 2. Gather UI values
                string path = _resourcePath;

                // 3. Initialize Logger
                await using var logWriter = new LogWriter("=== Model Scan started via UI ===");

                // 4. Offload heavy lifting
                var progress = new Progress<int>(percent => 
                {
                    RunModelScanButton.SetProgress(percent);
                });

                ModelScanner.ScanResults results = await Task.Run(() => 
                    ModelScanner.ScanDirectoryAsync(path, progress, logWriter));

                // 5. Handle Success
                ResultStatus.Text = results.CriticalFiles > 0 ? "Critical" : (results.WarningFiles > 0 ? "Warning" : "Safe");
                ResultStatus.Foreground = results.CriticalFiles > 0 ? new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x55, 0x55)) :
                                            results.WarningFiles > 0 ? new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xA5, 0x00)) :
                                            new SolidColorBrush(Color.FromArgb(0xFF, 0x4C, 0xFF, 0x70));

                ResultTotalMB.Text = $"{results.TotalFilesScanned} Scanned";
                ResultModelMB.Text = $"{results.FlaggedModels.Count} Flagged";

                if (results.FlaggedModels.Count > 0)
                {
                    var recs = new System.Collections.Generic.List<string> { "Check 'oversized_models_report.txt' in the folder for polygon/vertex details." };
                    foreach (var m in results.FlaggedModels.Take(5))
                        recs.Add($"Flagged: {m.FileName} ({m.HighestLODVertices:N0} vertices)");
                    
                    RecommendationsList.ItemsSource = recs;
                    RecommendationsList.Visibility = Visibility.Visible;
                    NoRecommendationsText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    NoRecommendationsText.Visibility = Visibility.Visible;
                }

                // 6. Professional Summary Alert
                string summaryMessage = $"Model analysis finished in {results.ScanDuration.TotalSeconds:F1} seconds.\n\n" +
                                        $"Total Scanned: {results.TotalFilesScanned}\n" +
                                        $"✅ Safe: {results.SafeFiles}\n" +
                                        $"⚠️ Warnings: {results.WarningFiles}\n" +
                                        $"🚨 Critical (Crash Risks): {results.CriticalFiles}";

                MessageBoxImage icon = (results.CriticalFiles > 0) ? MessageBoxImage.Error : 
                                       (results.WarningFiles > 0) ? MessageBoxImage.Warning : 
                                       MessageBoxImage.Information;

                if (results.CriticalFiles > 0 || results.WarningFiles > 0)
                {
                    summaryMessage += "\n\nA detailed report ('oversized_models_report.txt') has been saved to the scanned directory.";
                }

                MessageBox.Show(summaryMessage, "TGToolKit — Scan Complete", MessageBoxButton.OK, icon);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during model analysis:\n\n{ex.Message}", "TGToolKit — Scan Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 7. Always unlock
                SetButtonsEnabled(true);
                RunModelScanButton.Title = "Run Model Analysis";
                RunModelScanButton.ResetProgress();
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            bool ok = enabled && !string.IsNullOrWhiteSpace(_resourcePath) && Directory.Exists(_resourcePath);
            RunAuditButton.IsButtonEnabled = ok;
            RunModelScanButton.IsButtonEnabled = ok;
        }

        private void ResetDisplay()
        {
            ResultStatus.Text = "—";
            ResultStatus.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xE0, 0xFF, 0xFF));
            ResultTotalMB.Text = "—";
            ResultTextureMB.Text = "—";
            ResultModelMB.Text = "—";
            ResultCollisionMB.Text = "—";
            ResultAudioMB.Text = "—";
            RecommendationsList.ItemsSource = null;
            RecommendationsList.Visibility = Visibility.Collapsed;
            NoRecommendationsText.Visibility = Visibility.Collapsed;
        }
    }
}
