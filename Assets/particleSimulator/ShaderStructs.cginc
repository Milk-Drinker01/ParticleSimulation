#ifndef _STRUCTINCLUDE_
#define _STRUCTINCLUDE_

struct particleType
{
    float3 color;
    int randomColor;
};
struct particleData
{
    float3 color;
    float3 position;
    float3 velocity;
    int type;
};

#endif