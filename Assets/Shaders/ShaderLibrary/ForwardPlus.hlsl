#ifndef CUSTOM_FORWARDPLUS_INCLUDE
#define CUSTOM_FORWARDPLUS_INCLUDE

//xy : screen uv to tile uv
//z: tiles per row, int
//w: tile data size, int
float4 _ForwardPlusSettings;

StructuredBuffer<int> _ForwardPlusTiles;

struct ForwardPlusTile
{
    int2 coordinates;

    int index;

    int GetTileDataSize()
    {
        return asint(_ForwardPlusSettings.w);
    }

    int GetHeaderIndex()
    {
        return index * GetTileDataSize();
    }

    int GetLightCount()
    {
        return _ForwardPlusTiles[GetHeaderIndex()];
    }

    int GetFirstLightIndexInTile()
    {
        return GetHeaderIndex() + 1;
    }

    int GetLastLightIndexInTile()
    {
        return GetHeaderIndex() + GetLightCount();
    }

    //通过tileindex换成所有otherlight的lightindex
    int GetLightIndex(int lightIndexInTile)
    {
        return _ForwardPlusTiles[lightIndexInTile];
    }
};

//通过屏幕空间UV获取Tile结构
ForwardPlusTile GetForwardPlusTile(float2 screenUV)
{
    ForwardPlusTile tile;
    tile.coordinates = int2(screenUV * _ForwardPlusSettings.xy);
    tile.index = tile.coordinates.y * asint(_ForwardPlusSettings.z) + tile.coordinates.x;
    return tile;
}

#endif