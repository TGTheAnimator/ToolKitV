using System.IO;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.D3DCompiler;
using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace ToolKitV.Rendering
{
    public class Renderer : IDisposable
    {
        private Device _device;
        private DeviceContext _context;
        private Texture2D _renderTarget;
        private RenderTargetView _renderTargetView;
        private DX11ImageSource _imageSource;

        private VertexShader _vertexShader;
        private PixelShader _pixelShader;
        private InputLayout _inputLayout;
        private Buffer _vertexBuffer;
        private Buffer _constantBuffer;

        struct Constants
        {
            public Matrix WorldViewProj;
            public Matrix World;
            public Vector3 LightDir;
            public float Padding;
        }

        struct Vertex
        {
            public Vector3 Pos;
            public Vector3 Norm;
            public Vector2 Tex;
        }

        public DX11ImageSource ImageSource => _imageSource;

        public Renderer()
        {
            _imageSource = new DX11ImageSource();
            InitDX11();
            InitShaders();
            InitGeometry();
        }

        private void InitDX11()
        {
            _device = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            _context = _device.ImmediateContext;
        }

        private void InitShaders()
        {
            string shaderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "Rendering", "DefaultShader.hlsl");
            
            using var vsByteCode = ShaderBytecode.CompileFromFile(shaderPath, "VS", "vs_4_0");
            _vertexShader = new VertexShader(_device, vsByteCode);

            using var psByteCode = ShaderBytecode.CompileFromFile(shaderPath, "PS", "ps_4_0");
            _pixelShader = new PixelShader(_device, psByteCode);

            var signature = ShaderSignature.GetInputSignature(vsByteCode);
            _inputLayout = new InputLayout(_device, signature, new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 24, 0)
            });

            _constantBuffer = new Buffer(_device, Utilities.SizeOf<Constants>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        }

        private void InitGeometry()
        {
            var vertices = new[]
            {
                new Vertex { Pos = new Vector3(0.0f, 0.5f, 0.0f), Norm = new Vector3(0, 0, -1), Tex = new Vector2(0.5f, 0.0f) },
                new Vertex { Pos = new Vector3(0.5f, -0.5f, 0.0f), Norm = new Vector3(0, 0, -1), Tex = new Vector2(1.0f, 1.0f) },
                new Vertex { Pos = new Vector3(-0.5f, -0.5f, 0.0f), Norm = new Vector3(0, 0, -1), Tex = new Vector2(0.0f, 1.0f) }
            };

            _vertexBuffer = Buffer.Create(_device, BindFlags.VertexBuffer, vertices);
        }

        public void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0) return;

            // Clean up old render target
            _renderTargetView?.Dispose();
            _renderTarget?.Dispose();

            // Create new shared texture
            var texDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                Format = Format.B8G8R8A8_UNorm,
                ArraySize = 1,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                Usage = ResourceUsage.Default,
                CpuAccessFlags = CpuAccessFlags.None,
                MipLevels = 1,
                OptionFlags = ResourceOptionFlags.Shared,
                SampleDescription = new SampleDescription(1, 0)
            };

            _renderTarget = new Texture2D(_device, texDesc);
            _renderTargetView = new RenderTargetView(_device, _renderTarget);

            // Give it to the WPF interop
            _imageSource.SetRenderTarget(_renderTarget);
        }

        public void Render()
        {
            if (_renderTargetView == null) return;

            // Clear
            _context.ClearRenderTargetView(_renderTargetView, new Color4(0.1f, 0.12f, 0.15f, 1.0f));

            // Set up pipeline
            _context.InputAssembler.InputLayout = _inputLayout;
            _context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            _context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(_vertexBuffer, Utilities.SizeOf<Vertex>(), 0));

            _context.VertexShader.Set(_vertexShader);
            _context.VertexShader.SetConstantBuffer(0, _constantBuffer);
            _context.PixelShader.Set(_pixelShader);

            // Update constants (Simple orthographic projection for test)
            float aspect = _renderTarget.Description.Width / (float)_renderTarget.Description.Height;
            var view = Matrix.LookAtLH(new Vector3(0, 0, -5), Vector3.Zero, Vector3.Up);
            var proj = Matrix.OrthoLH(2 * aspect, 2, 0.1f, 100.0f);
            
            var constants = new Constants
            {
                World = Matrix.Identity,
                WorldViewProj = view * proj,
                LightDir = new Vector3(0, 0, 1) // pointing away from camera to light the front
            };
            _context.UpdateSubresource(ref constants, _constantBuffer);

            // Set viewport
            _context.Rasterizer.SetViewport(new Viewport(0, 0, _renderTarget.Description.Width, _renderTarget.Description.Height));
            _context.OutputMerger.SetTargets(_renderTargetView);

            // Draw
            _context.Draw(3, 0);

            // Force flush so D3D9 can see it
            _context.Flush();

            // Invalidate WPF image
            _imageSource.Invalidate();
        }

        public void Dispose()
        {
            _imageSource?.Dispose();
            _renderTargetView?.Dispose();
            _renderTarget?.Dispose();
            
            _vertexBuffer?.Dispose();
            _constantBuffer?.Dispose();
            _inputLayout?.Dispose();
            _vertexShader?.Dispose();
            _pixelShader?.Dispose();

            _context?.Dispose();
            _device?.Dispose();
        }
    }
}
