using System.IO;
using System.Windows;
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
        private Texture2D? _depthTarget;
        private DepthStencilView? _depthTargetView;
        private DX11ImageSource _imageSource;

        private VertexShader _vertexShader = null!;
        private PixelShader _pixelShader = null!;
        private InputLayout _inputLayout = null!;
        
        private Buffer _constantBuffer = null!;

        // Temporary storage for model geometries
        private struct GeometryBuffers
        {
            public Buffer VertexBuffer;
            public Buffer IndexBuffer;
            public int IndexCount;
            public int VertexStride;
        }
        private System.Collections.Generic.List<GeometryBuffers> _geometries = new();

        // Texture State
        private ShaderResourceView? _diffuseView;
        private SamplerState _samplerState = null!;
        private RasterizerState _rasterizerState = null!;

        // Camera State
        public float CameraPitch { get; set; } = 0.5f;
        public float CameraYaw { get; set; } = 0.5f;
        public float CameraDistance { get; set; } = 5.0f;
        private Vector3 _modelCenter = Vector3.Zero;
        private float _modelRadius = 1.0f;

        struct Constants
        {
            public Matrix WorldViewProj;
            public Matrix World;
            public Vector3 LightDir;
            public uint HasTexture;
            public Vector4 Padding; // Use Vector4 (16 bytes) to ensure 16-byte alignment (Total: 64+64+12+4+16 = 160)

            public Constants()
            {
                WorldViewProj = Matrix.Identity;
                World = Matrix.Identity;
                LightDir = Vector3.Zero;
                HasTexture = 0;
                Padding = Vector4.Zero;
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
            InitRasterizer();
        }

        private void InitRasterizer()
        {
            var rsDesc = new RasterizerStateDescription
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None, // Cull None for prototype to ensure visibility
                IsFrontCounterClockwise = false,
                IsDepthClipEnabled = true
            };
            _rasterizerState = new RasterizerState(_device, rsDesc);
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

        public void LoadDrawable(DrawableBase drawable)
        {
            // Clean up old model
            foreach (var g in _geometries)
            {
                g.VertexBuffer.Dispose();
                g.IndexBuffer.Dispose();
            }
            _geometries.Clear();

            if (drawable == null) return;

            // Set camera targets
            if (drawable is Drawable d)
            {
                _modelCenter = d.BoundingCenter;
                _modelRadius = Math.Max(0.1f, d.BoundingSphereRadius);
            }
            CameraDistance = _modelRadius * 2.5f;

            if (drawable.DrawableModels == null) return;

            // Try to find any available LOD
            DrawableModel[]? models = null;
            if (drawable.DrawableModels.High != null && drawable.DrawableModels.High.Length > 0) models = drawable.DrawableModels.High;
            else if (drawable.DrawableModels.Med != null && drawable.DrawableModels.Med.Length > 0) models = drawable.DrawableModels.Med;
            else if (drawable.DrawableModels.Low != null && drawable.DrawableModels.Low.Length > 0) models = drawable.DrawableModels.Low;

            if (models == null || models.Length == 0) return;

            foreach (var lod in models)
            {
                if (lod.Geometries == null) continue;
                foreach (var geom in lod.Geometries)
                {
                    var vb = geom.VertexBuffer;
                    var ib = geom.IndexBuffer;

                    if (vb?.Data1 == null || ib?.Indices == null) continue;

                    // Manual Decompression into clean format
                    var vertices = new Vertex[vb.VertexCount];
                    var vd = vb.Data1;
                    for (int v = 0; v < vb.VertexCount; v++)
                    {
                        vertices[v] = new Vertex
                        {
                            Pos = vd.GetVector3(v, (int)VertexSemantics.Position),
                            Norm = vd.GetVector3(v, (int)VertexSemantics.Normal),
                            Tex = vd.GetVector2(v, (int)VertexSemantics.TexCoord0)
                        };
                    }

                    var gb = new GeometryBuffers
                    {
                        VertexBuffer = Buffer.Create(_device, BindFlags.VertexBuffer, vertices),
                        IndexBuffer = Buffer.Create(_device, BindFlags.IndexBuffer, ib.Indices),
                        IndexCount = ib.Indices.Length,
                        VertexStride = Utilities.SizeOf<Vertex>()
                    };
                    _geometries.Add(gb);
                }
            }

            if (_geometries.Count > 0)
            {
                // MessageBox.Show($"Loaded {_geometries.Count} geometries.\nTotal indices: {_geometries.Sum(g => g.IndexCount)}", "Renderer Debug", MessageBoxButton.OK, MessageBoxImage.Information);
            }

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

            // Create depth buffer
            var depthDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                Format = Format.D24_UNorm_S8_UInt,
                ArraySize = 1,
                BindFlags = BindFlags.DepthStencil,
                Usage = ResourceUsage.Default,
                CpuAccessFlags = CpuAccessFlags.None,
                MipLevels = 1,
                OptionFlags = ResourceOptionFlags.None,
                SampleDescription = new SampleDescription(1, 0)
            };
            _depthTarget = new Texture2D(_device, depthDesc);
            _depthTargetView = new DepthStencilView(_device, _depthTarget);

            // Give it to the WPF interop
            _imageSource.SetRenderTarget(_renderTarget);
        }

        public void Render()
        {
            if (_renderTargetView == null || _depthTargetView == null) return;

            // Clear
            _context.ClearRenderTargetView(_renderTargetView, new Color4(0.1f, 0.12f, 0.15f, 1.0f));
            _context.ClearDepthStencilView(_depthTargetView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);

            if (_geometries.Count > 0)
            {
                // Set up pipeline
                _context.InputAssembler.InputLayout = _inputLayout;
                _context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
                
                _context.VertexShader.Set(_vertexShader);
                _context.VertexShader.SetConstantBuffer(0, _constantBuffer);
                _context.PixelShader.Set(_pixelShader);
                _context.PixelShader.SetShaderResource(0, _diffuseView);
                _context.PixelShader.SetSampler(0, _samplerState);

                // Update constants
                if (_renderTarget == null) return;
                float aspect = (float)_renderTarget.Description.Width / _renderTarget.Description.Height;

                // Calculate camera position based on pitch/yaw/distance
                float cy = (float)Math.Cos(CameraYaw);
                float sy = (float)Math.Sin(CameraYaw);
                float cp = (float)Math.Cos(CameraPitch);
                float sp = (float)Math.Sin(CameraPitch);

                Vector3 camPos = _modelCenter + new Vector3(
                    CameraDistance * sy * cp,
                    CameraDistance * sp,
                    CameraDistance * cy * cp
                );
 
                var view = Matrix.LookAtLH(camPos, _modelCenter, Vector3.Up);
                var proj = Matrix.PerspectiveFovLH((float)Math.PI / 4.0f, aspect, 0.01f, 10000.0f);
                 
                var constants = new Constants
                {
                    World = Matrix.Identity,
                    WorldViewProj = view * proj,
                    LightDir = new Vector3(0, -1, 1), // basic directional light
                    HasTexture = _diffuseView != null ? 1u : 0u
                };
                _context.UpdateSubresource(ref constants, _constantBuffer);

                // Set state
                _context.Rasterizer.State = _rasterizerState;
                _context.Rasterizer.SetViewport(new Viewport(0, 0, _renderTarget.Description.Width, _renderTarget.Description.Height));
                _context.OutputMerger.SetTargets(_depthTargetView, _renderTargetView);

                foreach (var g in _geometries)
                {
                    _context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(g.VertexBuffer, g.VertexStride, 0));
                    _context.InputAssembler.SetIndexBuffer(g.IndexBuffer, Format.R16_UInt, 0);
                    _context.DrawIndexed(g.IndexCount, 0, 0);
                }
            }

            // Force flush so D3D9 can see it
            _context.Flush();

            // Invalidate WPF image
            _imageSource.Invalidate();
        }

        public void Dispose()
        {
            _imageSource.Dispose();
            foreach (var g in _geometries)
            {
                g.VertexBuffer.Dispose();
                g.IndexBuffer.Dispose();
            }
            _geometries.Clear();
            _constantBuffer?.Dispose();
            _vertexShader?.Dispose();
            _pixelShader?.Dispose();
            _inputLayout?.Dispose();
            _samplerState?.Dispose();
            _rasterizerState?.Dispose();
            _diffuseView?.Dispose();
            _renderTargetView?.Dispose();
            _renderTarget?.Dispose();
            _depthTargetView?.Dispose();
            _depthTarget?.Dispose();
            _device?.Dispose();
            _context?.Dispose();
        }
    }
}
