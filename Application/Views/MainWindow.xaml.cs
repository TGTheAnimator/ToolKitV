using System.Windows;
using System.Windows.Input;
using ToolKitV.Views;

namespace ToolKitV
{
    public partial class MainWindow : Window
    {
        // Pre-instantiate views so state is preserved when switching tabs
        private readonly TextureOptimization _textureView  = new();
        private readonly VehicleTools        _vehicleView  = new();

        public MainWindow()
        {
            InitializeComponent();

            // Start on Texture Optimizer
            MainContent.Content = _textureView;

            // Wire up menu navigation
            SideMenu.NavigateTo += OnNavigateTo;
        }

        private void OnNavigateTo(string view)
        {
            switch (view)
            {
                case "TextureOptimizer":
                    MainContent.Content  = _textureView;
                    AppSubtitle.Text     = "  FiveM Texture Optimizer";
                    break;

                case "VehicleTools":
                    MainContent.Content  = _vehicleView;
                    AppSubtitle.Text     = "  Vehicle Meta Consolidation";
                    break;
            }
        }

        private void StackPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
