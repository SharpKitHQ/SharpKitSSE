cbuffer SceneConstants : register(b0)
{
    row_major float4x4 WorldViewProjection;
    float4 LightDirection;
    float4 CameraPosition;
    float4 AmbientLightColor;
    float4 DirectionalLightColor;
};

Texture2D DiffuseTexture : register(t0);
SamplerState DiffuseSampler : register(s0);

struct VSInput
{
    float3 Position    : POSITION;
    float3 Normal      : NORMAL;
    float2 TexCoord    : TEXCOORD;
    float4 Tint        : COLOR;
};

struct VSOutput
{
    float4 Position    : SV_POSITION;
    float3 Normal      : NORMAL;
    float3 WorldPos    : TEXCOORD0;
    float2 TexCoord    : TEXCOORD1;
    float4 Tint        : COLOR;
};

VSOutput VSMain(VSInput input)
{
    VSOutput output;
    output.Position = mul(float4(input.Position, 1.0f), WorldViewProjection);
    output.Normal = input.Normal;
    output.WorldPos = input.Position;
    output.TexCoord = input.TexCoord;
    output.Tint = input.Tint;
    return output;
}

float4 PSMain(VSOutput input) : SV_TARGET
{
    float3 n = normalize(input.Normal);
    float3 l = normalize(LightDirection.xyz);

    // Simple diffuse lighting
    float diff = max(dot(n, l), 0.0f);
    float3 ambient = AmbientLightColor.rgb;
    float3 lightColor = DirectionalLightColor.rgb * diff;
    float3 lighting = ambient + lightColor;

    // Sample the diffuse texture (falls back to white if no texture bound)
    float4 texel = DiffuseTexture.Sample(DiffuseSampler, input.TexCoord);
    clip(texel.a - 0.01f);

    const float exposure = 1.2f;
    return float4(texel.rgb * input.Tint.rgb * lighting * exposure, texel.a * input.Tint.a);
}
