struct VS_IN
{
    float3 pos : POSITION;
    float3 norm : NORMAL;
    float2 tex : TEXCOORD;
};

struct PS_IN
{
    float4 pos : SV_POSITION;
    float3 norm : NORMAL;
    float2 tex : TEXCOORD;
};

cbuffer Constants : register(b0)
{
    matrix WorldViewProj;
    matrix World;
    float3 LightDir;
    bool HasTexture;
};

Texture2D DiffuseTexture : register(t0);
SamplerState LinearSampler : register(s0);

PS_IN VS(VS_IN input)
{
    PS_IN output = (PS_IN)0;
    
    // Transform position
    output.pos = mul(float4(input.pos, 1.0f), WorldViewProj);
    
    // Pass normal and texcoord
    output.norm = mul(input.norm, (float3x3)World);
    output.tex = input.tex;
    
    return output;
}

float4 PS(PS_IN input) : SV_Target
{
    // Basic directional lighting
    float3 normal = normalize(input.norm);
    float light = saturate(dot(normal, -normalize(LightDir))) * 0.8 + 0.2;
    
    float4 color = float4(0.7, 0.7, 0.7, 1.0); // Default grey
    
    if (HasTexture)
    {
        color = DiffuseTexture.Sample(LinearSampler, input.tex);
    }
    
    return float4(color.rgb * light, color.a);
}
