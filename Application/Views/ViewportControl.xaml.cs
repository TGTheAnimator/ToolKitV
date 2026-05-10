using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ToolKitV.Rendering;

namespace ToolKitV.Views
{
    public partial class ViewportControl : UserControl, IDisposable
    {
        private Renderer _renderer;
        private bool _isActive = false;

        public ViewportControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            try
            {
                _renderer = new Renderer();
                ViewportImage.Source = _renderer.ImageSource.ImageSource;
                
                // Hook into WPF rendering loop for 60fps
                CompositionTarget.Rendering += CompositionTarget_Rendering;
                
                _isActive = true;
                UpdateSize();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize 3D Viewport: {ex.Message}", "DirectX Error", MessageBoxButton.OK, MessageBoxImage.Error);
                OverlayBorder.Visibility = Visibility.Visible;
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateSize();
        }

        private void UpdateSize()
        {
            if (_isActive && _renderer != null && ActualWidth > 0 && ActualHeight > 0)
            {
                _renderer.Resize((int)ActualWidth, (int)ActualHeight);
            }
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (_isActive && _renderer != null)
            {
                // In a real scenario, we'd check if the scene actually needs redrawing 
                // to save power. For now, continuous render.
                _renderer.Render();
            }
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            OverlayBorder.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        }

        public void Dispose()
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            _isActive = false;
            
            if (_renderer != null)
            {
                ViewportImage.Source = null;
                _renderer.Dispose();
                _renderer = null;
            }
        }
    }
}
