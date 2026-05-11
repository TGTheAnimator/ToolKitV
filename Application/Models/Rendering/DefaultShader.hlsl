// TGToolKit — DefaultShader.hlsl
// VS_IN declares ONLY what the shader reads.
// This ensures the compiled input signature matches our InputLayout exactly.

cbuffer SceneConstants : register(b0)
{
    matrix WorldViewProj;
    float3 LightDir;
    float  HasTexture;
    float4 Ambient;
};

Texture2D    DiffuseTexture : register(t0);
SamplerState LinearSampler  : register(s0);

struct VS_IN
{
    float3 Position : POSITION;   // always present in GTA V verts
    float4 Normal   : NORMAL;     // float4 handles both Float3 and R8G8B8A8_SNorm
    float2 TexCoord : TEXCOORD;   // TEXCOORD semantic index 0
};

struct PS_IN
{
    float4 pos  : SV_POSITION;
    float3 norm : NORMAL;
    float2 tex  : TEXCOORD0;
};

PS_IN VS(VS_IN input)
{
    PS_IN output;
    output.pos  = mul(float4(input.Position, 1.0f), WorldViewProj);
    output.norm = input.Normal.xyz;
    output.tex  = input.TexCoord;
    return output;
}

float4 PS(PS_IN input) : SV_Target
{
    float3 n     = normalize(input.norm);
    float  NdotL = saturate(dot(n, -normalize(LightDir)));
    float  light = NdotL * 0.8f + 0.2f;

    float4 albedo = (HasTexture > 0.5f)
        ? DiffuseTexture.Sample(LinearSampler, input.tex)
        : float4(0.72f, 0.72f, 0.72f, 1.0f);

    return float4(albedo.rgb * light + Ambient.rgb * 0.15f, albedo.a);
}
