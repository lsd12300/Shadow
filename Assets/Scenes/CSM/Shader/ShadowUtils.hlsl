
#ifndef SHADOW_UTILS_INCLUDE
#define SHADOW_UTILS_INCLUDE


#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#define _DIRECTIONAL_PCF3       // 定义软阴影混合采样次数


#if defined(_DIRECTIONAL_PCF3)
#define DIRECTIONAL_FILTER_SAMPLERS 4
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
#define DIRECTIONAL_FILTER_SAMPLERS 9
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
#define DIRECTIONAL_FILTER_SAMPLERS 16
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif


// 阴影图集.  (SAMPLER_CMP 特殊采样器, 常规的双线性过滤对深度没意义)
//  Tip: 只有一种合适的方法可以对阴影贴图采样, 所以直接定义采样器状态
TEXTURE2D_SHADOW(_CascadeShadowTex);
#define SHADOW_SAMPLER sampler_CascadeShadowTex_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);
//TEXTURE2D(_CascadeShadowTex);
//SAMPLER(sampler_CascadeShadowTex);
float4 _CascadeShadowTex_ST;
float4 _CascadeShadowTex_TexelSize;

float _CascadeCount;
float4x4 _LightSpaceCascadeVs[4];      // 世界空间 到 光源投影空间
float4x4 _LightSpaceCascadePs[4];      // 世界空间 到 光源投影空间
float4x4 _LightSpaceCascadeVPs[4];
float _CascadeSplitDises[4];            // 划分级联的距离
float _LightSpaceZLens[4];
float4 _LightBoundingSphere[4];         // 包围球数据
float4x4 _ShadowTexUVMatrix[4];




// 阴影贴图必须是ShadowMap格式:  cmd.GetTemporaryRT(id, 1024, 2014, 32, UnityEngine.FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
float UnityPCF3x3ShadowAPI(float3 posSTS) {
    real weights[DIRECTIONAL_FILTER_SAMPLERS];        // 如: 3x3 共 9像素, 4次采样
    real2 poses[DIRECTIONAL_FILTER_SAMPLERS];
    DIRECTIONAL_FILTER_SETUP(_CascadeShadowTex_TexelSize, posSTS.xy, weights, poses);
    float shadow = 0.0;
    for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLERS; i++)
    {
        shadow += weights[i] * SAMPLE_TEXTURE2D_SHADOW(_CascadeShadowTex, SHADOW_SAMPLER, float3(poses[i].xy, posSTS.z));
    }

    return shadow;
}



float GetShadowAABB(float3 worldPos/*, float3 cameraViewPos*/) {
    // 计算级联等级
    // 1. 根据相机空间计算.  (优点: 消耗小.  缺点: 级联等级不够精确, 阴影锯齿严重)
    //float cascadeIndex = 0;
    //for (int index = 0; index < _CascadeSplitDises.Length; index++)
    //{
    //    cascadeIndex = index;
    //    if (abs(cameraViewPos.z) <= _CascadeSplitDises[index]) break;
    //}

    // 2. 根据光源视锥体判断.  (优点: 级联等级精确, 阴影质量高.   缺点: 计算量大)
    float cascadeIndex = 0;
    float4 lightViewPos;
    for (uint index = 0; index < _LightSpaceZLens.Length; index++)
    {
        cascadeIndex = index;
        lightViewPos = mul(_LightSpaceCascadeVs[index], float4(worldPos, 1.0));
        if (abs(lightViewPos.z) <= _LightSpaceZLens[index]) break;
    }

    // 光源空间的 屏幕位置
    //float3 lightViewPos = mul(_LightSpaceCascadeVs[cascadeIndex], float4(worldPos, 1.0));
    float4 lightClipPos = mul(_LightSpaceCascadePs[cascadeIndex], float4(lightViewPos.xyz, 1.0));
    float4 lightScreenPos = ComputeScreenPos(lightClipPos);

    //float3 viewportOffset = float3((cascadeIndex % 2) * 0.5, floor(cascadeIndex / 2) * 0.5, 0.0);
    //float3 depthUV = 0.5 * lightScreenPos.xyz / lightScreenPos.w + viewportOffset;
    float perAreaSize = _CascadeShadowTex_ST.w / 2.0;
    float horizontalCount = _CascadeCount / 2.0;
    float3 viewportOffset = float3(0, 0, 0);
    float3 uvViewportScale = float3(1, 1, 1);
    if (_CascadeCount > 1)
    {
        viewportOffset = float3((cascadeIndex % horizontalCount) / horizontalCount, floor(cascadeIndex / horizontalCount) * 0.5, 0.0);
        uvViewportScale.x /= horizontalCount;
        uvViewportScale.y *= 0.5;
    }

    float3 depthUV = uvViewportScale * lightScreenPos.xyz / lightScreenPos.w + viewportOffset;


//    //float lightSpaceDepth = SAMPLE_TEXTURE2D_SHADOW(_CascadeShadowTex, SHADOW_SAMPLER, depthUV);
//    float lightSpaceDepth = SAMPLE_DEPTH_TEXTURE(_CascadeShadowTex, sampler_CascadeShadowTex, depthUV.xy).r;
//#if UNITY_REVERSED_Z
//    float shadow = step(lightScreenPos.z / lightScreenPos.w, lightSpaceDepth);
//#else
//    float shadow = step(lightSpaceDepth, lightScreenPos.z / lightScreenPos.w);      // 深度越大, 离相机越远(被其他近的遮挡)
//#endif

    // Untiy自带接口
    float shadow = UnityPCF3x3ShadowAPI(depthUV);

    return shadow;
}


float GetShadowSphere(float3 worldPos/*, float3 cameraViewPos*/) {
    // 计算级联等级
    float cascadeIndex = 0;
    float3 disVec;
    for (uint index = 0; index < _LightBoundingSphere.Length; index++)
    {
        cascadeIndex = index;
        disVec = _LightBoundingSphere[index].xyz - worldPos.xyz;
        if (disVec.x * disVec.x + disVec.y * disVec.y + disVec.z * disVec.z <= _LightBoundingSphere[index].w) break;
    }

    // 光源空间的 屏幕位置
    //float4 lightViewPos = mul(_LightSpaceCascadeVs[cascadeIndex], float4(worldPos, 1.0));
    //float4 lightClipPos = mul(_LightSpaceCascadePs[cascadeIndex], float4(lightViewPos.xyz, 1.0));
    float4 lightClipPos = mul(_LightSpaceCascadeVPs[cascadeIndex], float4(worldPos, 1.0));
    float4 lightScreenPos = ComputeScreenPos(lightClipPos);

    float perAreaSize = _CascadeShadowTex_ST.w / 2.0;
    float horizontalCount = _CascadeCount / 2.0;
    float3 viewportOffset = float3(0, 0, 0);
    float3 uvViewportScale = float3(1, 1, 1);
    if (_CascadeCount > 1) 
    {
        viewportOffset = float3((cascadeIndex % horizontalCount) / horizontalCount, floor(cascadeIndex / horizontalCount) * 0.5, 0.0);
        uvViewportScale.x /= horizontalCount;
        uvViewportScale.y *= 0.5;
    }

    float3 depthUV = uvViewportScale * lightScreenPos.xyz / lightScreenPos.w + viewportOffset;

    //float lightSpaceDepth = SAMPLE_DEPTH_TEXTURE(_CascadeShadowTex, sampler_CascadeShadowTex, depthUV.xy).r;
    //float lightSpaceDepth = SAMPLE_TEXTURE2D_SHADOW(_CascadeShadowTex, SHADOW_SAMPLER, depthUV);
//    float depth = lightScreenPos.z / lightScreenPos.w;
//#if UNITY_REVERSED_Z
//    float shadow = step(depth, lightSpaceDepth);
//#else
//    float shadow = step(lightSpaceDepth, depth);      // 深度越大, 离相机越远(被其他近的遮挡)
//#endif


    // Untiy自带接口
    float shadow = UnityPCF3x3ShadowAPI(depthUV);


    return shadow;
}



#endif // SHADOW_UTILS_INCLUDE