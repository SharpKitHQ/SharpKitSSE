cbuffer SceneConstants : register(b0)
{
    row_major float4x4 WorldViewProjection;
    float4 LightDirection;
    float4 CameraPosition;
    float4 AmbientLightColor;
    float4 DirectionalLightColor;
    float4 AmbientLightCube[6];
    float4 PointLightCount;
    float4 PointLightPositionRadius[32];
    float4 PointLightColorFalloff[32];
};

Texture2D DiffuseTexture : register(t0);
Texture2D EmissiveTexture : register(t1);
Texture2D EnvironmentMaskTexture : register(t2);
TextureCube EnvironmentTexture : register(t3);
SamplerState DiffuseSampler : register(s0);

float3 EvaluateAmbient(float3 normal);
float3 EvaluateDirectLight(float3 normal, float3 viewDirection, float3 lightDirection,
    float3 radiance, float3 specularColor, float specularStrength, float glossiness);
float3 SrgbToLinear(float3 color);
float3 LinearToSrgb(float3 color);

struct VSInput
{
    float3 Position    : POSITION;
    float3 Normal      : NORMAL;
    float2 TexCoord    : TEXCOORD;
    float4 Tint        : COLOR;
    float4 Specular    : TEXCOORD2;
    float4 Emissive    : TEXCOORD3;
    float4 Material    : TEXCOORD4;
};

struct VSOutput
{
    float4 Position    : SV_POSITION;
    float3 Normal      : NORMAL;
    float3 WorldPos    : TEXCOORD0;
    float2 TexCoord    : TEXCOORD1;
    float4 Tint        : COLOR;
    float4 Specular    : TEXCOORD2;
    float4 Emissive    : TEXCOORD3;
    float4 Material    : TEXCOORD4;
};

VSOutput VSMain(VSInput input)
{
    VSOutput output;
    output.Position = mul(float4(input.Position, 1.0f), WorldViewProjection);
    output.Normal = input.Normal;
    output.WorldPos = input.Position;
    output.TexCoord = input.TexCoord;
    output.Tint = input.Tint;
    output.Specular = input.Specular;
    output.Emissive = input.Emissive;
    output.Material = input.Material;
    return output;
}

float4 PSMain(VSOutput input) : SV_TARGET
{
    if (input.Material.z > 0.5f)
    {
        float2 worldPosition = input.WorldPos.xy;
        float2 minorCoordinates = worldPosition;
        float2 majorCoordinates = worldPosition * 0.1f;
        float2 minorDistance = abs(frac(minorCoordinates - 0.5f) - 0.5f);
        float2 majorDistance = abs(frac(majorCoordinates - 0.5f) - 0.5f);
        float minorLine = 1.0f - smoothstep(
            0.035f, 0.035f + max(fwidth(minorCoordinates.x), fwidth(minorCoordinates.y)),
            min(minorDistance.x, minorDistance.y));
        float majorLine = 1.0f - smoothstep(
            0.035f, 0.035f + max(fwidth(majorCoordinates.x), fwidth(majorCoordinates.y)),
            min(majorDistance.x, majorDistance.y));
        float gridAlpha = max(minorLine * 0.5f, majorLine * 0.95f);
        clip(gridAlpha - 0.01f);

        float3 gridColor = lerp(float3(0.52f, 0.54f, 0.56f),
            float3(0.82f, 0.84f, 0.86f), majorLine);
        return float4(gridColor, gridAlpha);
    }

    float3 n = normalize(input.Normal);
    float4 texel = DiffuseTexture.Sample(DiffuseSampler, input.TexCoord);
    clip(texel.a - 0.01f);

    float3 albedo = SrgbToLinear(texel.rgb) * input.Tint.rgb;
    float3 viewDirection = normalize(CameraPosition.xyz - input.WorldPos);
    float3 lighting = EvaluateAmbient(n);

    float3 directionalDirection = normalize(LightDirection.xyz);
    lighting += EvaluateDirectLight(
        n, viewDirection, directionalDirection, DirectionalLightColor.rgb,
        input.Specular.rgb, input.Specular.a, input.Material.x);

    [loop]
    for (uint i = 0; i < (uint)PointLightCount.x; ++i)
    {
        float3 toLight = PointLightPositionRadius[i].xyz - input.WorldPos;
        float distanceToLight = length(toLight);
        float radius = max(PointLightPositionRadius[i].w, 0.001f);
        if (distanceToLight >= radius)
            continue;

        float normalizedDistance = distanceToLight / radius;
        float rangeFade = saturate(1.0f - normalizedDistance);
        float exponent = max(PointLightColorFalloff[i].w, 0.1f);
        float attenuation = pow(rangeFade, exponent) /
                            (1.0f + 4.0f * normalizedDistance * normalizedDistance);
        float3 pointRadiance = PointLightColorFalloff[i].rgb * attenuation;
        lighting += EvaluateDirectLight(
            n, viewDirection, normalize(toLight), pointRadiance,
            input.Specular.rgb, input.Specular.a, input.Material.x);
    }

    float3 emissiveMask = input.Material.y > 0.5f
        ? EmissiveTexture.Sample(DiffuseSampler, input.TexCoord).rgb
        : float3(1.0f, 1.0f, 1.0f);
    float3 emissive = input.Emissive.rgb * input.Emissive.a * emissiveMask;
    float3 linearColor = albedo * lighting * 1.35f + emissive;
    if (input.Material.w > 0.0f)
    {
        float environmentMask = EnvironmentMaskTexture.Sample(DiffuseSampler, input.TexCoord).r;
        float3 reflectionDirection = reflect(-viewDirection, n);
        float3 environmentColor = EnvironmentTexture.Sample(DiffuseSampler, reflectionDirection).rgb;
        linearColor += SrgbToLinear(environmentColor) * environmentMask * input.Material.w;
    }
    float3 mappedColor = linearColor / (1.0f + linearColor);
    float3 outputColor = LinearToSrgb(mappedColor);

    // Keep the texture and material alpha for the existing alpha blend pass.
    return float4(outputColor, texel.a * input.Tint.a);
}

float3 EvaluateAmbient(float3 normal)
{
    float3 ambient =
        AmbientLightCube[0].rgb * max(normal.x, 0.0f) +
        AmbientLightCube[1].rgb * max(-normal.x, 0.0f) +
        AmbientLightCube[2].rgb * max(normal.y, 0.0f) +
        AmbientLightCube[3].rgb * max(-normal.y, 0.0f) +
        AmbientLightCube[4].rgb * max(normal.z, 0.0f) +
        AmbientLightCube[5].rgb * max(-normal.z, 0.0f);
    float weight = abs(normal.x) + abs(normal.y) + abs(normal.z);
    ambient /= max(weight, 0.001f);

    // A missing directional ambient cube falls back to the cell's flat ambient color.
    if (dot(ambient, float3(1.0f, 1.0f, 1.0f)) < 0.0001f)
        ambient = AmbientLightColor.rgb;
    return ambient;
}

float3 EvaluateDirectLight(float3 normal, float3 viewDirection, float3 lightDirection,
    float3 radiance, float3 specularColor, float specularStrength, float glossiness)
{
    float diffuse = max(dot(normal, lightDirection), 0.0f);
    float3 halfDirection = normalize(lightDirection + viewDirection);
    float specular = pow(max(dot(normal, halfDirection), 0.0f), max(glossiness, 1.0f)) *
                     max(specularStrength, 0.0f);
    return radiance * (diffuse + specularColor * specular);
}

float3 SrgbToLinear(float3 color)
{
    float3 low = color / 12.92f;
    float3 high = pow((color + 0.055f) / 1.055f, 2.4f);
    float3 lowMask = 1.0f - step(0.04045f, color);
    return lerp(high, low, lowMask);
}

float3 LinearToSrgb(float3 color)
{
    color = max(color, 0.0f);
    float3 low = color * 12.92f;
    float3 high = 1.055f * pow(color, 1.0f / 2.4f) - 0.055f;
    float3 lowMask = 1.0f - step(0.0031308f, color);
    return lerp(high, low, lowMask);
}
