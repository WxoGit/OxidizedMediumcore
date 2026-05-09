sampler2D baseTexture : register(s0);

float globalTime;
float3 teamColor;
float2 textureSize;
float2 tileUV;
float borderFade;
float pixelSize;
float area;
float tileBorderWhite;

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float2 pixel = tileUV / 8;
    float2 texelIndex = floor(coords / pixel);
    float2 texelCount = floor(1.0 / pixel);
    float2 pixelatedCoords = (texelIndex + 0.5) / texelCount;

    float fadeX = min(pixelatedCoords.x, 1.0 - pixelatedCoords.x) / borderFade;
    float fadeY = min(pixelatedCoords.y, 1.0 - pixelatedCoords.y) / borderFade;

    float sx = saturate(fadeX);
    float sy = saturate(fadeY);

    float smoothDist = 1.0 - length(float2(1.0 - sx, 1.0 - sy));
    float onBorder = 1.0 - smoothstep(0.0, 1.0, saturate(smoothDist));

    float borderPixels = 1.0;

    float edgeX = step(texelIndex.x, borderPixels - 1.0) + step(texelCount.x - borderPixels, texelIndex.x);
    float edgeY = step(texelIndex.y, borderPixels - 1.0) + step(texelCount.y - borderPixels, texelIndex.y);
    float hardEdge = saturate(edgeX + edgeY);

    float2 withinTile = frac(coords / tileUV);
    float tileEdgeX = step(withinTile.x, borderPixels / 8.0) + step(1.0 - borderPixels / 16.0, withinTile.x);
    float tileEdgeY = step(withinTile.y, borderPixels / 8.0) + step(1.0 - borderPixels / 16.0, withinTile.y);
    float tileEdge = saturate(tileEdgeX + tileEdgeY);

    float anyEdge = saturate(hardEdge + tileEdge);

    float cornerX = step(texelIndex.x, 0.0) + step(texelCount.x - 1.0, texelIndex.x);
    float cornerY = step(texelIndex.y, 0.0) + step(texelCount.y - 1.0, texelIndex.y);
    float isCorner = saturate(cornerX) * saturate(cornerY);

    float vibrance = 1.0;
    float luma = dot(teamColor, float3(0.299, 0.587, 0.114));
    float3 vividColor = lerp(float3(luma, luma, luma), teamColor, vibrance);
    float edgeStrength = lerp(0.6, 1.0, hardEdge);
    float3 col = lerp(vividColor, float3(1, 1, 1), hardEdge * edgeStrength);
    col = lerp(col, float3(1, 1, 1), tileEdge * tileBorderWhite);
    float alpha = sampleColor.a * onBorder * (1.0 - isCorner);
    return float4(col * alpha, alpha);
}

technique t0
{
    pass p0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}