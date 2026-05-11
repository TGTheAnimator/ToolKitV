// TGToolKit — DefaultShader.hlsl
// Declares all semantics used by GTA V vertex formats (matching CodeWalker's VertexTypes.cs).
// The VS only consumes POSITION, NORMAL, and TEXCOORD0 — all other inputs are passed through
// so DirectX can match any VertexType's InputLayout without compilation errors.

cbuffer SceneConstants : register(b0)
{
    matrix WorldViewProj;
    float3 LightDir;
    float  HasTexture;
    float4 Ambient;
};

Texture2D    DiffuseTexture : register(t0);
SamplerState LinearSampler  : register(s0);

// -----------------------------------------------------------------------
// Vertex Input — all GTA V semantic slots declared
// -----------------------------------------------------------------------
struct VS_IN
{
    float3 Position      : POSITION;
    float4 BlendWeights  : BLENDWEIGHTS;   // optional — skinned meshes
    uint4  BlendIndices  : BLENDINDICES;   // optional — skinned meshes
    float3 Normal        : NORMAL;
    float4 Color0        : COLOR0;
    float4 Color1        : COLOR1;
    float2 TexCoord0     : TEXCOORD0;
    float2 TexCoord1     : TEXCOORD1;
    float2 TexCoord2     : TEXCOORD2;
    float2 TexCoord3     : TEXCOORD3;
    float4 Tangent       : TANGENT;
    float4 Binormal      : BINORMAL;
};

struct PS_IN
{
    float4 pos  : SV_POSITION;
    float3 norm : NORMAL;
    float2 tex  : TEXCOORD0;
};

// -----------------------------------------------------------------------
// Vertex Shader
// -----------------------------------------------------------------------
PS_IN VS(VS_IN input)
{
    PS_IN output;
    output.pos  = mul(float4(input.Position, 1.0f), WorldViewProj);
    output.norm = input.Normal;
    output.tex  = input.TexCoord0;
    return output;
}

// -----------------------------------------------------------------------
// Pixel Shader — directional lighting + optional texture
// -----------------------------------------------------------------------
float4 PS(PS_IN input) : SV_Target
{
    float3 n     = normalize(input.norm);
    float  NdotL = saturate(dot(n, -normalize(LightDir)));
    float  light = NdotL * 0.8f + 0.2f;  // diffuse + ambient floor

    float4 albedo = (HasTexture > 0.5f)
        ? DiffuseTexture.Sample(LinearSampler, input.tex)
        : float4(0.72f, 0.72f, 0.72f, 1.0f);  // neutral grey for untextured

    return float4(albedo.rgb * light + Ambient.rgb * 0.15f, albedo.a);
}
