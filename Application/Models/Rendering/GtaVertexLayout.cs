// Ported from CodeWalker's VertexTypes.cs — dexyfex/CodeWalker (MIT License)
// This dynamically builds a DirectX InputElement[] from GTA V's VertexType flags + VertexDeclarationTypes.
// CodeWalker uploads raw VertexBytes to the GPU and uses this to create a matching InputLayout per geometry.
using CodeWalker.GameFiles;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Collections.Generic;
using System.Linq;

namespace ToolKitV.Rendering
{
    public static class GtaVertexLayout
    {
        // Maps the 16 semantic slots to HLSL semantic names (matches CodeWalker exactly)
        private static readonly string[] Semantics = new string[16]
        {
            "POSITION",      // 0
            "BLENDWEIGHTS",  // 1
            "BLENDINDICES",  // 2
            "NORMAL",        // 3
            "COLOR",         // 4  COLOR0
            "COLOR",         // 5  COLOR1
            "TEXCOORD",      // 6  TEXCOORD0
            "TEXCOORD",      // 7  TEXCOORD1
            "TEXCOORD",      // 8  TEXCOORD2
            "TEXCOORD",      // 9  TEXCOORD3
            "TEXCOORD",      // 10 TEXCOORD4
            "TEXCOORD",      // 11 TEXCOORD5
            "TEXCOORD",      // 12 TEXCOORD6
            "TEXCOORD",      // 13 TEXCOORD7
            "TANGENT",       // 14
            "BINORMAL",      // 15
        };

        public static Format GetDxgiFormat(VertexComponentType type)
        {
            return type switch
            {
                VertexComponentType.Half2       => Format.R16G16_Float,
                VertexComponentType.Float       => Format.R32_Float,
                VertexComponentType.Half4       => Format.R16G16B16A16_Float,
                VertexComponentType.Float2      => Format.R32G32_Float,
                VertexComponentType.Float3      => Format.R32G32B32_Float,
                VertexComponentType.Float4      => Format.R32G32B32A32_Float,
                VertexComponentType.UByte4      => Format.R8G8B8A8_UInt,
                VertexComponentType.Colour      => Format.R8G8B8A8_UNorm,
                VertexComponentType.RGBA8SNorm  => Format.R8G8B8A8_SNorm,
                _                               => Format.Unknown,
            };
        }

        /// <summary>
        /// Builds an InputElement array matching the raw vertex byte layout for a given GTA V geometry.
        /// Ported directly from CodeWalker's VertexTypeGTAV.GetLayout().
        /// </summary>
        public static InputElement[] GetLayout(VertexType componentsFlags, VertexDeclarationTypes componentsTypes)
        {
            var inputElements = new List<InputElement>();
            var types = (ulong)componentsTypes;
            var flags = (uint)componentsFlags;
            int offset = 0;

            for (int k = 0; k < 16; k++)
            {
                if (((flags >> k) & 0x1) == 1)
                {
                    var componentType = (VertexComponentType)((types >> (k * 4)) & 0x0F);
                    if (componentType == VertexComponentType.Nothing) continue;

                    var format = GetDxgiFormat(componentType);
                    int size = VertexComponentTypes.GetSizeInBytes(componentType);
                    if (size == 0 || format == Format.Unknown) { offset += size; continue; }

                    // Count how many elements with this semantic already exist to get the index
                    int index = inputElements.Count(e => e.SemanticName == Semantics[k]);
                    inputElements.Add(new InputElement(Semantics[k], index, format, offset, 0));
                    offset += size;
                }
            }

            return inputElements.ToArray();
        }
    }
}
