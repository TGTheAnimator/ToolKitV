using System;
using System.Windows.Interop;
using SharpDX.Direct3D9;
using SharpDX.Direct3D11;
using System.Windows;
using Texture = SharpDX.Direct3D11.Texture2D;

namespace ToolKitV.Rendering
{
    public class DX11ImageSource : IDisposable
    {
        private D3DImage _d3dImage;
        private Direct3DEx _d3dContext;
        private DeviceEx _d3dDevice;
        private Texture _renderTarget;

        public D3DImage ImageSource => _d3dImage;

        public DX11ImageSource()
        {
            _d3dImage = new D3DImage();
            InitD3D9();
        }

        private void InitD3D9()
        {
            _d3dContext = new Direct3DEx();

            var presentparams = new PresentParameters
            {
                Windowed = true,
                SwapEffect = SwapEffect.Discard,
                DeviceWindowHandle = GetDesktopWindow(),
                PresentationInterval = PresentInterval.Default
            };

            _d3dDevice = new DeviceEx(_d3dContext, 0, DeviceType.Hardware, IntPtr.Zero, 
                CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve, 
                presentparams);
        }

        public void SetRenderTarget(Texture renderTarget)
        {
            if (_renderTarget == renderTarget)
                return;

            _renderTarget = renderTarget;

            if (_renderTarget == null)
            {
                _d3dImage.Lock();
                _d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
                _d3dImage.Unlock();
                return;
            }

            using (var resource = _renderTarget.QueryInterface<SharpDX.DXGI.Resource>())
            {
                var handle = resource.SharedHandle;
                if (handle == IntPtr.Zero)
                    throw new ArgumentException("Texture must be created with ResourceOptionFlags.Shared");

                using (var texture = new SharpDX.Direct3D9.Texture(_d3dDevice, _renderTarget.Description.Width, _renderTarget.Description.Height, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default, ref handle))
                using (var surface = texture.GetSurfaceLevel(0))
                {
                    _d3dImage.Lock();
                    _d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, surface.NativePointer);
                    _d3dImage.Unlock();
                }
            }
        }

        public void Invalidate()
        {
            if (_renderTarget != null)
            {
                _d3dImage.Lock();
                _d3dImage.AddDirtyRect(new Int32Rect(0, 0, _d3dImage.PixelWidth, _d3dImage.PixelHeight));
                _d3dImage.Unlock();
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        public void Dispose()
        {
            SetRenderTarget(null);
            _d3dDevice?.Dispose();
            _d3dContext?.Dispose();
        }
    }
}
