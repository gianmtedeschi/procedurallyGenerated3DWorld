#ifndef _MC_COMMON
#define _MC_COMMON

struct VERTEX
{
    float3 p;
    float3 n;
};

struct TRIANGLE {
    VERTEX v[3];
};

struct TRIANGLE_LIST {
    TRIANGLE tris[5];
};

struct GRIDCELL {
    float3 p[8];
    float4 val[8];
};

struct NOISE_LAYER_PARAMS
{
    float3 offset;
    float influence;
    float3 scale;
    float height;
    float warpInfluence;
    float3 warpScale;
};

struct POISSON_CELL_DELIMITER
{
    int startIndex;
    int endIndex;
};

struct POISSON_POINT
{
    float2 position;
    float radius;
};

float3 GetCoords(uint x, uint y, uint z, float scale, uint resolution, float3 offset)
{
    return float3
        (
            (float(x) / resolution) * scale,
            (float(y) / resolution) * scale,
            (float(z) / resolution) * scale
            ) + offset;
}

float SampleSDFLinear(Texture3D<float4> tex, SamplerState samplerState, float3 coords)
{

    return (tex.SampleLevel(samplerState, coords, 0)).r;
}

float3 GetSDFCoords(float3 coords, float scale, int splits)
{
    float3 texCoordsF = float3
        ((coords.x) / (scale * splits),
            (coords.y) / (scale * splits),
            (coords.z) / (scale * splits));

    return texCoordsF;
}
#endif