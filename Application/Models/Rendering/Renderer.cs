using System.IO;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.D3DCompiler;
using CodeWalker.GameFiles;
using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace ToolKitV.Rendering
{
    public class Renderer : IDisposable
    {
        private Device _device = null!;
        private DeviceContext _context = null!;
        private Texture2D? _renderTarget;
        private RenderTargetView? _renderTargetView;
        private DX11ImageSource _imageSource;

        private VertexShader _vertexShader = null!;
        private PixelShader _pixelShader = null!;
        private InputLayout _inputLayout = null!;
        
        private Buffer _constantBuffer = null!;

        // Temporary storage for single model prototype
        private Buffer? _modelVertexBuffer;
        private Buffer? _modelIndexBuffer;
        private int _modelIndexCount;
        private int _vertexStride;

        // Texture State
        private ShaderResourceView? _diffuseView;
        private SamplerState _samplerState = null!;

        // Camera State
        public float CameraPitch { get; set; } = 0.5f;
        public float CameraYaw { get; set; } = 0.5f;
        public float CameraDistance { get; set; } = 5.0f;

        struct Constants
        {
            public Matrix WorldViewProj;
            public Matrix World;
            public Vector3 LightDir;
            public uint HasTexture;
            public Vector3 Padding;

            public Constants()
            {
                WorldViewProj = Matrix.Identity;
                World = Matrix.Identity;
                LightDir = Vector3.Zero;
                HasTexture = 0;
                Padding = Vector3.Zero;
            }
        }

        struct Vertex
        {
            public Vector3 Pos;
            public Vector3 Norm;
            public Vector2 Tex;
            
            public Vertex()
            {
                Pos = Vector3.Zero;
                Norm = Vector3.Zero;
                Tex = Vector2.Zero;
            }
        }

        public DX11ImageSource ImageSource => _imageSource;

        public Renderer()
        {
            _imageSource = new DX11ImageSource();
            InitDX11();
            InitShaders();
            InitSampler();
        }

        private void InitSampler()
        {
            var samplerDesc = new SamplerStateDescription
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                ComparisonFunction = Comparison.Never,
                MinimumLod = 0,
                MaximumLod = float.MaxValue
            };
            _samplerState = new SamplerState(_device, samplerDesc);
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

        public void LoadDrawable(Drawable drawable)
        {
            // Clean up old model
            _modelVertexBuffer?.Dispose();
            _modelIndexBuffer?.Dispose();
            _modelVertexBuffer = null;
            _modelIndexBuffer = null;
            _modelIndexCount = 0;

            if (drawable?.DrawableModels?.High == null || drawable.DrawableModels.High.Length == 0) return;

            // For prototype, we just grab the first geometry of the first LOD
            var lod = drawable.DrawableModels.High[0];
            if (lod.Geometries == null || lod.Geometries.Length == 0) return;

            var geom = lod.Geometries[0];
            var vb = geom.VertexBuffer;
            var ib = geom.IndexBuffer;

            if (vb?.Data1?.VertexBytes == null || ib?.Indices == null) return;

            _vertexStride = vb.VertexStride;

            // Create Vertex Buffer from raw bytes
            _modelVertexBuffer = Buffer.Create(_device, BindFlags.VertexBuffer, vb.Data1.VertexBytes);

            // Create Index Buffer from ushort array
            _modelIndexBuffer = Buffer.Create(_device, BindFlags.IndexBuffer, ib.Indices);
            _modelIndexCount = ib.Indices.Length;

            // Force rendering update
            Render();
        }

        public void LoadTexture(CodeWalker.GameFiles.Texture cwTex)
        {
            _diffuseView?.Dispose();
            _diffuseView = null;

            if (cwTex?.Data?.FullData == null) return;

            Format format = Format.Unknown;
            switch (cwTex.Format)
            {
                case TextureFormat.D3DFMT_DXT1: format = Format.BC1_UNorm; break;
                case TextureFormat.D3DFMT_DXT3: format = Format.BC2_UNorm; break;
                case TextureFormat.D3DFMT_DXT5: format = Format.BC3_UNorm; break;
                case TextureFormat.D3DFMT_ATI1: format = Format.BC4_UNorm; break;
                case TextureFormat.D3DFMT_ATI2: format = Format.BC5_UNorm; break;
                case TextureFormat.D3DFMT_BC7:  format = Format.BC7_UNorm; break;
                case TextureFormat.D3DFMT_A8R8G8B8: format = Format.B8G8R8A8_UNorm; break;
            }

            if (format == Format.Unknown) return;

            try
            {
                // Create the texture resource
                var texDesc = new Texture2DDescription
                {
                    Width = cwTex.Width,
                    Height = cwTex.Height,
                    MipLevels = cwTex.Levels,
                    ArraySize = 1,
                    Format = format,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.ShaderResource,
                    CpuAccessFlags = CpuAccessFlags.None,
                    OptionFlags = ResourceOptionFlags.None
                };

                // Fill mip data
                using (var dataStream = new DataStream(cwTex.Data.FullData.Length, true, true))
                {
                    dataStream.Write(cwTex.Data.FullData, 0, cwTex.Data.FullData.Length);
                    dataStream.Position = 0;

                    var dataRects = new DataRectangle[cwTex.Levels];
                    int offset = 0;
                    for (int i = 0; i < cwTex.Levels; i++)
                    {
                        int mipWidth = Math.Max(1, cwTex.Width >> i);
                        int mipHeight = Math.Max(1, cwTex.Height >> i);
                        
                        int rowPitch = 0;
                        int slicePitch = 0;

                        // Calculate pitch for block compressed or raw
                        if (format >= Format.BC1_Typeless && format <= Format.BC5_SNorm || format == Format.BC7_Typeless || format == Format.BC7_UNorm || format == Format.BC7_UNorm_SRgb)
                        {
                            int blockWidth = (mipWidth + 3) / 4;
                            int blockHeight = (mipHeight + 3) / 4;
                            int blockSize = (format == Format.BC1_UNorm || format == Format.BC4_UNorm) ? 8 : 16;
                            rowPitch = blockWidth * blockSize;
                            slicePitch = rowPitch * blockHeight;
                        }
                        else
                        {
                            rowPitch = mipWidth * 4; // Assuming 32-bit for A8R8G8B8
                            slicePitch = rowPitch * mipHeight;
                        }

                        dataRects[i] = new DataRectangle(dataStream.DataPointer + offset, rowPitch);
                        offset += slicePitch;
                        if (offset > cwTex.Data.FullData.Length) break;
                    }

                    using var dxTex = new Texture2D(_device, texDesc, dataRects);
                    _diffuseView = new ShaderResourceView(_device, dxTex);
                }
            }
            catch (Exception)
            {
                // Fallback to no texture if creation fails
                _diffuseView = null;
            }

            Render();
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

            if (_modelVertexBuffer != null && _modelIndexBuffer != null)
            {
                // Set up pipeline
                _context.InputAssembler.InputLayout = _inputLayout;
                _context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
                _context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(_modelVertexBuffer, _vertexStride, 0));
                _context.InputAssembler.SetIndexBuffer(_modelIndexBuffer, Format.R16_UInt, 0);

                _context.VertexShader.Set(_vertexShader);
                _context.VertexShader.SetConstantBuffer(0, _constantBuffer);
                _context.PixelShader.Set(_pixelShader);
                _context.PixelShader.SetShaderResource(0, _diffuseView);
                _context.PixelShader.SetSampler(0, _samplerState);

                // Update constants (Orbital camera projection)
                if (_renderTarget == null) return;
                float aspect = (float)_renderTarget.Description.Width / _renderTarget.Description.Height;

                // Calculate camera position based on pitch/yaw/distance
                float cy = (float)Math.Cos(CameraYaw);
                float sy = (float)Math.Sin(CameraYaw);
                float cp = (float)Math.Cos(CameraPitch);
                float sp = (float)Math.Sin(CameraPitch);

                Vector3 camPos = new Vector3(
                    CameraDistance * sy * cp,
                    CameraDistance * sp,
                    CameraDistance * cy * cp
                );

                var view = Matrix.LookAtLH(camPos, Vector3.Zero, Vector3.Up);
                var proj = Matrix.PerspectiveFovLH((float)Math.PI / 4.0f, aspect, 0.1f, 1000.0f);
                
                var constants = new Constants
                {
                    World = Matrix.Identity,
                    WorldViewProj = view * proj,
                    LightDir = new Vector3(0, -1, 1), // basic directional light
                    HasTexture = _diffuseView != null ? 1u : 0u
                };
                _context.UpdateSubresource(ref constants, _constantBuffer);

                // Set viewport
                _context.Rasterizer.SetViewport(new Viewport(0, 0, _renderTarget.Description.Width, _renderTarget.Description.Height));
                _context.OutputMerger.SetTargets(_renderTargetView);

                // Draw
                _context.DrawIndexed(_modelIndexCount, 0, 0);
            }

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
            
            _modelVertexBuffer?.Dispose();
            _modelIndexBuffer?.Dispose();
            _diffuseView?.Dispose();
            _constantBuffer?.Dispose();
            _samplerState?.Dispose();
            _inputLayout?.Dispose();
            _vertexShader?.Dispose();
            _pixelShader?.Dispose();

            _context?.Dispose();
            _device?.Dispose();
        }
    }
}
