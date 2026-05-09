sampler2D baseTexture : register(s0);
float2 direction;
float gradientSharpness;
float cornerRadius;

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float2 distFromCenter = abs(coords - 0.5);

    float2 cornerOffset = max(distFromCenter - (0.5 - cornerRadius), 0.0);
    float distFromCornerCenter = length(cornerOffset);
    float mask = 1.0 - smoothstep(cornerRadius - 0.005, cornerRadius, distFromCornerCenter);

    float4 color = tex2D(baseTexture, coords) * sampleColor;
    color.a *= mask;

    float gradient = dot(coords - 0.5, direction) + 0.5;
    color.a *= 1.0 - saturate((gradient - 0.5) * gradientSharpness + 0.5);
    color.rgb *= color.a;

    return color;
}

technique t0
{
    pass p0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}