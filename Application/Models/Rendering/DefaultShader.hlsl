// TGToolKit — DefaultShader.hlsl (Uber-Shader with permutations)

cbuffer SceneConstants : register(b0) {
    matrix WorldViewProj;
    float3 LightDir;
    float  HasTexture;
    float4 Ambient;
};

Texture2D    DiffuseTexture : register(t0);
SamplerState LinearSampler  : register(s0);

struct VS_IN {
    float3 Position : POSITION;
#ifdef HAS_NORMAL
    float4 Normal   : NORMAL;
#endif
#ifdef HAS_TEXCOORD
    float2 TexCoord : TEXCOORD;
#endif
};

struct PS_IN {
    float4 pos  : SV_POSITION;
    float3 norm : NORMAL;
    float2 tex  : TEXCOORD0;
};

PS_IN VS(VS_IN input) {
    PS_IN output;
    output.pos  = mul(float4(input.Position, 1.0f), WorldViewProj);

#ifdef HAS_NORMAL
    output.norm = input.Normal.xyz;
#else
    output.norm = float3(0.0f, 1.0f, 0.0f); // Default pointing up if no normals exist
#endif

#ifdef HAS_TEXCOORD
    output.tex  = input.TexCoord;
#else
    output.tex  = float2(0.0f, 0.0f); // Default UV
#endif

    return output;
}

float4 PS(PS_IN input) : SV_Target {
    float3 n = normalize(input.norm);
    
    // Lighting setup
    float3 keyDir   = normalize(float3(-0.6f, -1.0f, 0.5f));
    float  key      = saturate(dot(n, -keyDir)) * 0.75f;
    float3 fillDir  = normalize(float3(1.0f, -0.3f, -0.5f));
    float  fill     = saturate(dot(n, -fillDir)) * 0.25f;
    float  light    = key + fill + 0.15f;

    float4 albedo = (HasTexture > 0.5f)
        ? DiffuseTexture.Sample(LinearSampler, input.tex)
        : float4(0.78f, 0.78f, 0.78f, 1.0f);

    float3 lit = albedo.rgb * light + Ambient.rgb * 0.2f;
    return float4(saturate(lit), albedo.a);
}
