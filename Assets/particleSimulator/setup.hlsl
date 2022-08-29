#ifndef _SYSTEMSETUP_
#define _SYSTEMSETUP_

#include "ShaderStructs.cginc"

//#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
StructuredBuffer<particleData> _particleData;

float4x4 getPostionMatrix(float3 position)
{
    return float4x4(    1,0,0,0,
                        0,1,0,0,
                        0,0,1,0,
                        position.x, position.y, position.z, 1);
}

#define unity_ObjectToWorld unity_ObjectToWorld
#define unity_WorldToObject unity_WorldToObject
void setup()
{
    float3 data = _particleData[unity_InstanceID].position;
    
    unity_ObjectToWorld._11_21_31_41 = float4(1, 0, 0, 0);
    unity_ObjectToWorld._12_22_32_42 = float4(0, 1, 0, 0);
    unity_ObjectToWorld._13_23_33_43 = float4(0, 0, 1, 0);
    unity_ObjectToWorld._14_24_34_44 = float4(data, 1);
    
    unity_WorldToObject = unity_ObjectToWorld;
    unity_WorldToObject._14_24_34 *= -1;
    unity_WorldToObject._11_22_33 = 1.0f / unity_WorldToObject._11_22_33;
}

#endif



float3 GetInstancedColor()
{
    float3 Out;
    Out = 0;
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
#ifndef SHADERGRAPH_PREVIEW
    Out = _particleData[unity_InstanceID].color;
#endif
#endif
    return Out;
}

#endif