using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ToolKitV.Views
{
    public partial class Menu : UserControl
    {
        /// <summary>
        /// Fired when the user selects a tool. Payload is a view key:
        /// "TextureOptimizer" | "VehicleTools"
        /// </summary>
        public event Action<string>? NavigateTo;

        private string _activeView = "TextureOptimizer";

        public Menu()
        {
            InitializeComponent();
            SetActiveItem("TextureOptimizer");
        }

        // ── Click handlers ────────────────────────────────────────────────────

        private void TextureOptimizer_Click(object sender, MouseButtonEventArgs e)
        {
            if (_activeView == "TextureOptimizer") return;
            SetActiveItem("TextureOptimizer");
            NavigateTo?.Invoke("TextureOptimizer");
        }

        private void Vehicles_Click(object sender, MouseButtonEventArgs e)
        {
            if (_activeView == "VehicleTools") return;
            SetActiveItem("VehicleTools");
            NavigateTo?.Invoke("VehicleTools");
        }

        // ── Visual state ──────────────────────────────────────────────────────

        private void SetActiveItem(string view)
        {
            _activeView = view;

            bool texActive      = view == "TextureOptimizer";
            bool vehiclesActive = view == "VehicleTools";

            // Texture Optimizer item
            TextureOptimizerBg.Visibility         = texActive ? Visibility.Visible   : Visibility.Collapsed;
            TextureOptimizerInactiveBg.Visibility = texActive ? Visibility.Collapsed : Visibility.Visible;
            TextureOptimizerStripe.Visibility     = texActive ? Visibility.Visible   : Visibility.Collapsed;
            TextureOptimizerLabel.FontWeight      = texActive ? FontWeights.Bold     : FontWeights.Normal;
            TextureOptimizerLabel.Foreground      = texActive
                ? new SolidColorBrush(Colors.White)
                : new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));

            // Vehicles item
            VehiclesBg.Visibility         = vehiclesActive ? Visibility.Visible   : Visibility.Collapsed;
            VehiclesInactiveBg.Visibility = vehiclesActive ? Visibility.Collapsed : Visibility.Visible;
            VehiclesStripe.Visibility     = vehiclesActive ? Visibility.Visible   : Visibility.Collapsed;
            VehiclesLabel.FontWeight      = vehiclesActive ? FontWeights.Bold     : FontWeights.Normal;
            VehiclesLabel.Foreground      = vehiclesActive
                ? new SolidColorBrush(Colors.White)
                : new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
        }
    }
}
