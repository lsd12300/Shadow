
#ifndef SHADOW_UTILS_INCLUDE
#define SHADOW_UTILS_INCLUDE



TEXTURE2D(_CascadeShadowTex);
SAMPLER(sampler_CascadeShadowTex);
float4 _CascadeShadowTex_ST;
float4 _CascadeShadowTex_TexelSize;

float _CascadeCount;
float4x4 _LightSpaceCascadeVs[4];      // 世界空间 到 光源投影空间
float4x4 _LightSpaceCascadePs[4];      // 世界空间 到 光源投影空间
float4x4 _LightSpaceCascadeVPs[4];
float _CascadeSplitDises[4];            // 划分级联的距离
float _LightSpaceZLens[4];
float4 _LightBoundingSphere[4];         // 包围球数据



float PCF3x3(float2 uv, float2 texSizeR, float lightZ) {
    float val = 0.0;
    for (int i = -1; i <= 1; i++) {
        for (int j = -1; j <= 1; j++) {
            float2 sampleUV = uv + texSizeR * float2(i, j);
            #if UNITY_REVERSED_Z
                val += step(lightZ, SAMPLE_DEPTH_TEXTURE(_CascadeShadowTex, sampler_CascadeShadowTex, sampleUV).r);
            #else
                val += step(SAMPLE_DEPTH_TEXTURE(_CascadeShadowTex, sampler_CascadeShadowTex, sampleUV).r, lightZ);      // 深度越大, 离相机越远(被其他近的遮挡)
            #endif
        }
    }
    return val / 9.0;
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
    for (int index = 0; index < _LightSpaceZLens.Length; index++)
    {
        cascadeIndex = index;
        lightViewPos = mul(_LightSpaceCascadeVs[index], float4(worldPos, 1.0));
        if (abs(lightViewPos.z) <= _LightSpaceZLens[index]) break;
    }

    // 光源空间的 屏幕位置
    //float3 lightViewPos = mul(_LightSpaceCascadeVs[cascadeIndex], float4(worldPos, 1.0));
    float4 lightClipPos = mul(_LightSpaceCascadePs[cascadeIndex], float4(lightViewPos.xyz, 1.0));
    float4 lightScreenPos = ComputeScreenPos(lightClipPos);
    float2 viewportOffset = float2((cascadeIndex % 2) * 0.5, floor(cascadeIndex / 2) * 0.5);
    float2 depthUV = 0.5 * lightScreenPos.xy / lightScreenPos.w + viewportOffset;
    float lightSpaceDepth = SAMPLE_DEPTH_TEXTURE(_CascadeShadowTex, sampler_CascadeShadowTex, depthUV).r;


#if UNITY_REVERSED_Z
    float shadow = step(lightScreenPos.z / lightScreenPos.w, lightSpaceDepth);
#else
    float shadow = step(lightSpaceDepth, lightScreenPos.z / lightScreenPos.w);      // 深度越大, 离相机越远(被其他近的遮挡)
#endif

    return shadow;
}


float GetShadowSphere(float3 worldPos/*, float3 cameraViewPos*/) {
    // 计算级联等级
    float cascadeIndex = 0;
    float3 disVec;
    for (int index = 0; index < _LightBoundingSphere.Length; index++)
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
    float2 viewportOffset = float2(0,0);
    float2 uvViewportScale = float2(1, 1);
    if (_CascadeCount > 1) 
    {
        viewportOffset = float2((cascadeIndex % horizontalCount) / horizontalCount, floor(cascadeIndex / horizontalCount) * 0.5);
        uvViewportScale.x /= horizontalCount;
        uvViewportScale.y *= 0.5;
    }

    float2 depthUV = uvViewportScale * lightScreenPos.xy / lightScreenPos.w + viewportOffset;

    float lightSpaceDepth = SAMPLE_DEPTH_TEXTURE(_CascadeShadowTex, sampler_CascadeShadowTex, depthUV).r;
#if UNITY_REVERSED_Z
    float shadow = step(lightScreenPos.z / lightScreenPos.w, lightSpaceDepth);
#else
    float shadow = step(lightSpaceDepth, lightScreenPos.z / lightScreenPos.w);      // 深度越大, 离相机越远(被其他近的遮挡)
#endif

    //float shadow = PCF3x3(depthUV, _CascadeShadowTex_TexelSize.xy, lightScreenPos.z / lightScreenPos.w);

    return shadow;
}



#endif // SHADOW_UTILS_INCLUDE