using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ToolKitV.Rendering;

namespace ToolKitV.Views
{
    public partial class ViewportControl : UserControl, IDisposable
    {
        private Renderer? _renderer;
        private bool _isActive = false;

        private Point _lastMousePos;
        private bool _isDragging = false;

        // FPS calculation
        private Stopwatch _fpsStopwatch = new Stopwatch();
        private int _frameCount = 0;
        private double _lastFpsUpdate = 0;

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
                _fpsStopwatch.Start();
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
                // Multi-DPI Support: Calculate physical pixels
                var presentationSource = PresentationSource.FromVisual(this);
                double dpiX = 1.0;
                double dpiY = 1.0;

                if (presentationSource?.CompositionTarget != null)
                {
                    dpiX = presentationSource.CompositionTarget.TransformToDevice.M11;
                    dpiY = presentationSource.CompositionTarget.TransformToDevice.M22;
                }

                int pixelWidth = (int)(ActualWidth * dpiX);
                int pixelHeight = (int)(ActualHeight * dpiY);

                _renderer.Resize(pixelWidth, pixelHeight);
            }
        }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            if (_isActive && _renderer != null)
            {
                _renderer.Render();
                UpdateFps();
            }
        }

        private void UpdateFps()
        {
            _frameCount++;
            double elapsed = _fpsStopwatch.Elapsed.TotalSeconds;
            if (elapsed - _lastFpsUpdate >= 1.0)
            {
                double fps = _frameCount / (elapsed - _lastFpsUpdate);
                FpsText.Text = $"{fps:F0} FPS";
                _frameCount = 0;
                _lastFpsUpdate = elapsed;
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            _isDragging = true;
            _lastMousePos = e.GetPosition(this);
            CaptureMouse();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _isDragging = false;
            ReleaseMouseCapture();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging && _renderer != null)
            {
                var pos = e.GetPosition(this);
                var dx = pos.X - _lastMousePos.X;
                var dy = pos.Y - _lastMousePos.Y;

                _renderer.CameraYaw += (float)(dx * 0.01);
                _renderer.CameraPitch += (float)(dy * 0.01);
                
                // Clamp pitch to avoid gimbal lock
                _renderer.CameraPitch = Math.Max((float)-Math.PI / 2.1f, Math.Min((float)Math.PI / 2.1f, _renderer.CameraPitch));

                _lastMousePos = pos;
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_renderer != null)
            {
                _renderer.CameraDistance -= (float)(e.Delta * 0.01);
                _renderer.CameraDistance = Math.Max(0.5f, _renderer.CameraDistance);
            }
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            OverlayBorder.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
            if (active) _fpsStopwatch.Restart();
            else _fpsStopwatch.Stop();
        }

        public void LoadDrawable(CodeWalker.GameFiles.Drawable drawable)
        {
            _renderer?.LoadDrawable(drawable);
        }

        public void LoadTexture(CodeWalker.GameFiles.Texture texture)
        {
            _renderer?.LoadTexture(texture);
        }

        public void Dispose()
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            _isActive = false;
            _fpsStopwatch.Stop();
            
            if (_renderer != null)
            {
                ViewportImage.Source = null;
                _renderer.Dispose();
                _renderer = null;
            }
        }
    }
}
