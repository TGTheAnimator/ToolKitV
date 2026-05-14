// Ported from CodeWalker's VertexTypes.cs — dexyfex/CodeWalker (MIT License)
// Builds a DirectX InputElement[] from GTA V's VertexType flags + VertexDeclarationTypes.
// 
// KEY DESIGN: Our simple VS only reads POSITION, NORMAL, TEXCOORD0.
// We scan the full vertex declaration to find the exact byte offset of each needed
// component, then build InputElements pointing directly at those offsets.
// All other components are silently skipped — their bytes are still in the vertex
// buffer but D3D11 ignores them since the InputLayout doesn't reference them.
using CodeWalker.GameFiles;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Collections.Generic;

namespace ToolKitV.Rendering
{
    public static class GtaVertexLayout
    {
        // GTA V semantic slot → HLSL semantic name (16 slots, matches CodeWalker exactly)
        public static readonly string[] Semantics = new string[16]
        {
            "POSITION",      // 0
            "BLENDWEIGHTS",  // 1
            "BLENDINDICES",  // 2
            "NORMAL",        // 3
            "COLOR",         // 4  (COLOR0)
            "COLOR",         // 5  (COLOR1)
            "TEXCOORD",      // 6  (TEXCOORD0)
            "TEXCOORD",      // 7  (TEXCOORD1)
            "TEXCOORD",      // 8  (TEXCOORD2)
            "TEXCOORD",      // 9  (TEXCOORD3)
            "TEXCOORD",      // 10 (TEXCOORD4)
            "TEXCOORD",      // 11 (TEXCOORD5)
            "TEXCOORD",      // 12 (TEXCOORD6)
            "TEXCOORD",      // 13 (TEXCOORD7)
            "TANGENT",       // 14
            "BINORMAL",      // 15
        };

        public static Format GetDxgiFormat(VertexComponentType type)
        {
            return type switch
            {
                VertexComponentType.Float3     => Format.R32G32B32_Float,
                VertexComponentType.Float      => Format.R32_Float,
                VertexComponentType.Float4     => Format.R32G32B32A32_Float,
                VertexComponentType.Float2     => Format.R32G32_Float,
                VertexComponentType.UByte4     => Format.R8G8B8A8_UNorm,
                VertexComponentType.Dec3N      => Format.R10G10B10A2_UNorm, // Normal/Tangent compression
                VertexComponentType.Half2      => Format.R16G16_Float,      // 16-bit UVs
                VertexComponentType.Half4      => Format.R16G16B16A16_Float,
                VertexComponentType.Colour     => Format.B8G8R8A8_UNorm,
                VertexComponentType.RGBA8SNorm => Format.R8G8B8A8_SNorm,
                _                              => Format.Unknown,
            };
        }

        /// <summary>
        /// Builds InputElements for semantics our VS shader uses, while returning
        /// flags indicating which semantics were actually found in the geometry.
        /// </summary>
        public static (InputElement[]? Elements, bool HasNorm, bool HasTex) GetLayoutForSimpleShader(
            VertexType componentsFlags,
            VertexDeclarationTypes componentsTypes)
        {
            var types  = (ulong)componentsTypes;
            var flags  = (uint)componentsFlags;

            int offset = 0;
            var semanticCounts = new Dictionary<string, int>();
            var result = new List<InputElement>();

            bool hasNorm = false;
            bool hasTex  = false;

            for (int k = 0; k < 16; k++)
            {
                if (((flags >> k) & 0x1) == 0) continue;

                var compType = (VertexComponentType)((types >> (k * 4)) & 0x0F);
                if (compType == VertexComponentType.Nothing) continue;

                int size = VertexComponentTypes.GetSizeInBytes(compType);
                if (size == 0) continue;

                var semName = Semantics[k];
                semanticCounts.TryGetValue(semName, out int semIdx);
                semanticCounts[semName] = semIdx + 1;

                if (semName == "POSITION" && semIdx == 0) result.Add(CreateElem(semName, semIdx, compType, offset));
                if (semName == "NORMAL"   && semIdx == 0) { result.Add(CreateElem(semName, semIdx, compType, offset)); hasNorm = true; }
                if (semName == "TEXCOORD" && semIdx == 0) { result.Add(CreateElem(semName, semIdx, compType, offset)); hasTex  = true; }

                offset += size;
            }

            return result.Count > 0 ? (result.ToArray(), hasNorm, hasTex) : (null, false, false);
        }

        private static InputElement CreateElem(string name, int idx, VertexComponentType type, int offset)
        {
            return new InputElement(name, idx, GetDxgiFormat(type), offset, 0);
        }
    }
}

