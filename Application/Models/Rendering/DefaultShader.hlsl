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
    float3 n = normalize(input.norm);
    float3 l = normalize(LightDir);
    
    float diff = max(dot(n, l), 0.2f); // 0.2 ambient
    
    float3 color = float3(0.6f, 0.6f, 0.6f);
    
    if (HasTexture)
    {
        color = DiffuseTexture.Sample(LinearSampler, input.tex).rgb;
    }
    
    color *= diff;
    
    return float4(color, 1.0f);
}
