// TGToolKit — 3D Renderer (v2 — CodeWalker-accurate)
// Architecture mirrors CodeWalker's Renderable.cs:
//   - Raw VertexBytes uploaded directly to GPU (no manual decompression)
//   - Dynamic InputLayout per geometry based on VertexType flags
//   - Correct stride/topology from DrawableGeometry metadata
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using CodeWalker.GameFiles;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using ToolKitV.Rendering;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using Format = SharpDX.DXGI.Format;

namespace ToolKitV.Models.Rendering
{
    // -------------------------------------------------------------------------
    // Constant buffer — must be 16-byte aligned (padded to 256 is safest)
    // -------------------------------------------------------------------------
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    struct SceneConstants
    {
        [FieldOffset(0)]  public Matrix WorldViewProj;  // 64 bytes
        [FieldOffset(64)] public Vector3 LightDir;      // 12 bytes
        [FieldOffset(76)] public float   HasTexture;    //  4 bytes  (float bool)
        [FieldOffset(80)] public Vector4 Ambient;       // 16 bytes
    }

    // -------------------------------------------------------------------------
    // Per-geometry GPU resources
    // -------------------------------------------------------------------------
    class GeometryGpuData : IDisposable
    {
        public Buffer?      VertexBuffer;
        public Buffer?      IndexBuffer;
        public InputLayout? Layout;
        public int          VertexStride;
        public int          IndexCount;

        public void Dispose()
        {
            Layout?.Dispose();
            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();
        }
    }

    // -------------------------------------------------------------------------
    // Main Renderer
    // -------------------------------------------------------------------------
    public class Renderer : IDisposable
    {
        // DX resources
        private Device        _device;
        private DeviceContext _context;

        // Render target + depth
        private Texture2D?         _rtTex;
        private RenderTargetView?  _rtv;
        private Texture2D?         _depthTex;
        private DepthStencilView?  _dsv;

        // WPF interop
        private DX11ImageSource _imageSource;
        public DX11ImageSource ImageSource => _imageSource;

        // Pipeline
        private VertexShader?  _vs;
        private PixelShader?   _ps;
        private Buffer         _cb;
        private SamplerState   _sampler;
        private RasterizerState _rs;
        private byte[]?        _vsBlob; // kept for per-geom InputLayout creation

        // Texture
        private ShaderResourceView? _diffuseSrv;

        // Model
        private readonly List<GeometryGpuData> _geoms = new();

        // Camera
        public float CameraYaw      { get; set; } = 0.8f;
        public float CameraPitch    { get; set; } = 0.4f;
        public float CameraDistance { get; set; } = 5.0f;
        private Vector3 _modelCenter = Vector3.Zero;
        private float   _modelRadius = 1.0f;

        // -------------------------------------------------------------------------
        public Renderer()
        {
            _imageSource = new DX11ImageSource();

            // Create D3D11 device — no swap chain needed, we render into D3DImage
            _device = new Device(
                SharpDX.Direct3D.DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                new[] { SharpDX.Direct3D.FeatureLevel.Level_11_0,
                        SharpDX.Direct3D.FeatureLevel.Level_10_1,
                        SharpDX.Direct3D.FeatureLevel.Level_10_0 });
            _context = _device.ImmediateContext;

            InitPipeline();
        }

        private void InitPipeline()
        {
            // Constant buffer
            _cb = new Buffer(_device, Utilities.SizeOf<SceneConstants>(),
                ResourceUsage.Default, BindFlags.ConstantBuffer,
                CpuAccessFlags.None, ResourceOptionFlags.None, 0);

            // Shaders
            string hlslPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                           "Models", "Rendering", "DefaultShader.hlsl");
            string hlsl = File.ReadAllText(hlslPath);

            _vsBlob = SharpDX.D3DCompiler.ShaderBytecode.Compile(
                hlsl, "VS", "vs_5_0", SharpDX.D3DCompiler.ShaderFlags.None).Bytecode.Data;
            var psBlob = SharpDX.D3DCompiler.ShaderBytecode.Compile(
                hlsl, "PS", "ps_5_0", SharpDX.D3DCompiler.ShaderFlags.None).Bytecode.Data;

            _vs = new VertexShader(_device, _vsBlob);
            _ps = new PixelShader(_device, psBlob);

            // Sampler
            _sampler = new SamplerState(_device, new SamplerStateDescription
            {
                Filter             = Filter.MinMagMipLinear,
                AddressU           = TextureAddressMode.Wrap,
                AddressV           = TextureAddressMode.Wrap,
                AddressW           = TextureAddressMode.Wrap,
                ComparisonFunction = Comparison.Never,
                MinimumLod         = 0,
                MaximumLod         = float.MaxValue
            });

            // Rasterizer — cull back (standard), we can flip if needed
            _rs = new RasterizerState(_device, new RasterizerStateDescription
            {
                FillMode             = FillMode.Solid,
                CullMode             = CullMode.None,   // None until we confirm winding
                IsDepthClipEnabled   = true,
                IsScissorEnabled     = false,
                IsMultisampleEnabled = false
            });
        }

        // -------------------------------------------------------------------------
        // Resize render target
        // -------------------------------------------------------------------------
        public void Resize(int w, int h)
        {
            if (w <= 0 || h <= 0) return;

            _rtv?.Dispose();
            _rtTex?.Dispose();
            _dsv?.Dispose();
            _depthTex?.Dispose();

            _rtTex = new Texture2D(_device, new Texture2DDescription
            {
                Width             = w,
                Height            = h,
                MipLevels         = 1,
                ArraySize         = 1,
                Format            = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                BindFlags         = BindFlags.RenderTarget | BindFlags.ShaderResource,
                Usage             = ResourceUsage.Default,
                OptionFlags       = ResourceOptionFlags.Shared  // Required for DX11ImageSource / D3DImage interop
            });

            _rtv = new RenderTargetView(_device, _rtTex);

            _depthTex = new Texture2D(_device, new Texture2DDescription
            {
                Width             = w,
                Height            = h,
                MipLevels         = 1,
                ArraySize         = 1,
                Format            = Format.D24_UNorm_S8_UInt,
                SampleDescription = new SampleDescription(1, 0),
                BindFlags         = BindFlags.DepthStencil,
                Usage             = ResourceUsage.Default
            });
            _dsv = new DepthStencilView(_device, _depthTex);

            _imageSource.SetRenderTarget(_rtTex);
        }

        // -------------------------------------------------------------------------
        // Load model — mirrors CodeWalker's RenderableGeometry.Load()
        // -------------------------------------------------------------------------
        public void LoadDrawable(DrawableBase drawable)
        {
            ClearGeometries();
            if (drawable == null) return;

            // Camera framing
            if (drawable is Drawable d)
            {
                _modelCenter = d.BoundingCenter;
                _modelRadius = Math.Max(0.5f, d.BoundingSphereRadius);
                CameraDistance = _modelRadius * 2.8f;
            }

            // Walk all LOD levels — prioritise High
            var models = drawable.DrawableModels?.High
                      ?? drawable.DrawableModels?.Med
                      ?? drawable.DrawableModels?.Low
                      ?? drawable.AllModels;

            if (models == null) return;

            foreach (var model in models)
            {
                if (model?.Geometries == null) continue;
                foreach (var geom in model.Geometries)
                {
                    if (geom?.VertexData == null || geom.IndexBuffer?.Indices == null) continue;

                    var vd = geom.VertexData;
                    if (vd.VertexBytes == null || vd.VertexBytes.Length == 0) continue;

                    // --- CRITICAL: build InputLayout from geometry's own VertexType flags ---
                    var layoutElements = GtaVertexLayout.GetLayout(vd.VertexType, vd.Info.Types);
                    if (layoutElements.Length == 0) continue;

                    InputLayout? layout = null;
                    try
                    {
                        layout = new InputLayout(_device, _vsBlob, layoutElements);
                    }
                    catch { continue; } // skip incompatible layouts

                    // Upload raw bytes verbatim — exactly as CodeWalker does
                    var vbData = vd.VertexBytes;
                    var vbDesc = new BufferDescription(vbData.Length, ResourceUsage.Default,
                        BindFlags.VertexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, vd.VertexStride);
                    using var vbStream = new DataStream(vbData.Length, true, true);
                    vbStream.Write(vbData, 0, vbData.Length);
                    vbStream.Position = 0;
                    var vb = new Buffer(_device, vbStream, vbDesc);

                    var ib = Buffer.Create(_device, BindFlags.IndexBuffer, geom.IndexBuffer.Indices);

                    _geoms.Add(new GeometryGpuData
                    {
                        VertexBuffer = vb,
                        IndexBuffer  = ib,
                        Layout       = layout,
                        VertexStride = vd.VertexStride,
                        IndexCount   = geom.IndexBuffer.Indices.Length
                    });
                }
            }

            // Try to use embedded textures from ShaderGroup
            TryLoadEmbeddedTextures(drawable);

            Render();
        }

        private void TryLoadEmbeddedTextures(DrawableBase drawable)
        {
            if (_diffuseSrv != null) return; // already have an external texture

            var txd = (drawable as Drawable)?.ShaderGroup?.TextureDictionary;
            if (txd?.Textures?.data_items == null) return;

            var tex = txd.Textures.data_items.FirstOrDefault();
            if (tex != null) LoadTexture(tex);
        }

        // -------------------------------------------------------------------------
        // Load YTD texture — fixed mip pitch calculation
        // -------------------------------------------------------------------------
        public void LoadTexture(CodeWalker.GameFiles.Texture cwTex)
        {
            _diffuseSrv?.Dispose();
            _diffuseSrv = null;

            if (cwTex?.Data?.FullData == null || cwTex.Width <= 0 || cwTex.Height <= 0) return;

            Format fmt = cwTex.Format switch
            {
                TextureFormat.D3DFMT_DXT1     => Format.BC1_UNorm,
                TextureFormat.D3DFMT_DXT3     => Format.BC2_UNorm,
                TextureFormat.D3DFMT_DXT5     => Format.BC3_UNorm,
                TextureFormat.D3DFMT_ATI1     => Format.BC4_UNorm,
                TextureFormat.D3DFMT_ATI2     => Format.BC5_UNorm,
                TextureFormat.D3DFMT_BC7      => Format.BC7_UNorm,
                TextureFormat.D3DFMT_A8R8G8B8 => Format.B8G8R8A8_UNorm,
                TextureFormat.D3DFMT_A8B8G8R8 => Format.R8G8B8A8_UNorm,
                _                             => Format.Unknown
            };
            if (fmt == Format.Unknown) return;

            bool isBC = fmt >= Format.BC1_Typeless && fmt <= Format.BC7_UNorm_SRgb;

            try
            {
                int levels = Math.Max(1, (int)cwTex.Levels);
                var rects = new DataRectangle[levels];
                int offset = 0;
                var bytes = cwTex.Data.FullData;

                for (int i = 0; i < levels; i++)
                {
                    int mw = Math.Max(1, cwTex.Width  >> i);
                    int mh = Math.Max(1, cwTex.Height >> i);
                    int rowPitch, slicePitch;

                    if (isBC)
                    {
                        int blockSize = (fmt == Format.BC1_UNorm || fmt == Format.BC4_UNorm) ? 8 : 16;
                        int bw = Math.Max(1, (int)((mw + 3) / 4));
                        int bh = Math.Max(1, (int)((mh + 3) / 4));
                        rowPitch   = bw * blockSize;
                        slicePitch = rowPitch * bh;
                    }
                    else
                    {
                        rowPitch   = mw * 4;
                        slicePitch = rowPitch * mh;
                    }

                    if (offset + slicePitch > bytes.Length) break;

                    var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    rects[i] = new DataRectangle(pinned.AddrOfPinnedObject() + offset, rowPitch);
                    pinned.Free();

                    offset += slicePitch;
                }

                var texDesc = new Texture2DDescription
                {
                    Width             = cwTex.Width,
                    Height            = cwTex.Height,
                    MipLevels         = levels,
                    ArraySize         = 1,
                    Format            = fmt,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage             = ResourceUsage.Immutable,
                    BindFlags         = BindFlags.ShaderResource
                };

                using var tex2d = new Texture2D(_device, texDesc, rects);
                _diffuseSrv = new ShaderResourceView(_device, tex2d);
            }
            catch { /* silently skip bad textures */ }
        }

        // -------------------------------------------------------------------------
        // Render
        // -------------------------------------------------------------------------
        public void Render()
        {
            if (_rtv == null || _dsv == null) return;

            _context.ClearRenderTargetView(_rtv, new Color4(0.08f, 0.09f, 0.11f, 1f));
            _context.ClearDepthStencilView(_dsv, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);

            if (_geoms.Count == 0 || _rtTex == null) goto Flush;

            // --- Build camera matrices ---
            float aspect = (float)_rtTex.Description.Width / _rtTex.Description.Height;
            float cy = MathF.Cos(CameraYaw), sy = MathF.Sin(CameraYaw);
            float cp = MathF.Cos(CameraPitch), sp = MathF.Sin(CameraPitch);

            var camPos = _modelCenter + new Vector3(
                CameraDistance * sy * cp,
                CameraDistance * sp,
                CameraDistance * cy * cp);

            var view = Matrix.LookAtLH(camPos, _modelCenter, Vector3.Up);
            var proj = Matrix.PerspectiveFovLH(MathF.PI / 4f, aspect, 0.01f, _modelRadius * 500f);

            var sc = new SceneConstants
            {
                WorldViewProj = view * proj,
                LightDir      = Vector3.Normalize(new Vector3(0.5f, -1f, 0.7f)),
                HasTexture    = _diffuseSrv != null ? 1f : 0f,
                Ambient       = new Vector4(0.25f, 0.25f, 0.25f, 1f)
            };
            _context.UpdateSubresource(ref sc, _cb);

            // --- Set common state ---
            _context.VertexShader.Set(_vs);
            _context.VertexShader.SetConstantBuffer(0, _cb);
            _context.PixelShader.Set(_ps);
            _context.PixelShader.SetConstantBuffer(0, _cb);
            _context.PixelShader.SetShaderResource(0, _diffuseSrv);
            _context.PixelShader.SetSampler(0, _sampler);
            _context.Rasterizer.State = _rs;
            _context.Rasterizer.SetViewport(new Viewport(0, 0,
                _rtTex.Description.Width, _rtTex.Description.Height));
            _context.OutputMerger.SetTargets(_dsv, _rtv);
            _context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

            // --- Draw each geometry with its own InputLayout ---
            foreach (var g in _geoms)
            {
                if (g.Layout == null || g.VertexBuffer == null || g.IndexBuffer == null) continue;
                _context.InputAssembler.InputLayout = g.Layout;
                _context.InputAssembler.SetVertexBuffers(0,
                    new VertexBufferBinding(g.VertexBuffer, g.VertexStride, 0));
                _context.InputAssembler.SetIndexBuffer(g.IndexBuffer, Format.R16_UInt, 0);
                _context.DrawIndexed(g.IndexCount, 0, 0);
            }

            Flush:
            _context.Flush();
            _imageSource.Invalidate();
        }

        // -------------------------------------------------------------------------
        private void ClearGeometries()
        {
            foreach (var g in _geoms) g.Dispose();
            _geoms.Clear();
        }

        public void Dispose()
        {
            ClearGeometries();
            _diffuseSrv?.Dispose();
            _sampler.Dispose();
            _rs.Dispose();
            _cb.Dispose();
            _vs?.Dispose();
            _ps?.Dispose();
            _rtv?.Dispose();
            _rtTex?.Dispose();
            _dsv?.Dispose();
            _depthTex?.Dispose();
            _imageSource.Dispose();
            _context.Dispose();
            _device.Dispose();
        }
    }
}
