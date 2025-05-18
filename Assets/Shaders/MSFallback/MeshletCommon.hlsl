#ifndef MESHLET_COMMON_INCLUDED
#define MESHLET_COMMON_INCLUDED

#define MAX_PRIMS 126
#define MAX_VERTS 64
#define MS_GROUP_SIZE 128
#define AS_GROUP_SIZE 64

struct Meshlet
{
    uint VertOffset;
    uint PrimOffset;
    uint VertCount;
    uint PrimCount;
};

struct Vertex
{
    float3 Position;
};

#endif // MESHLET_COMMON_INCLUDED
