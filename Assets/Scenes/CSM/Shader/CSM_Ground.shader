Shader "Shadow/CSM_Ground"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {

        Pass
        {
            Tags { "LightMode" = "UniversalForward"  "RenderPipeline" = "UniversalPipeline" }
            //Blend One Zero


            HLSLPROGRAM

            #pragma enable_d3d11_debug_symbols
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ShadowUtils.hlsl"

            #pragma vertex shadow_vert
            #pragma fragment shadow_frag


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 viewSpacePos : TEXCOORD2;
                float4 screenSpacePos : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;


            v2f shadow_vert(appdata v)
            {
                v2f o;

                o.worldPos = TransformObjectToWorld(v.vertex.xyz);
                o.viewSpacePos = TransformWorldToView(o.worldPos);
                o.vertex = TransformWViewToHClip(o.viewSpacePos);
                o.screenSpacePos = ComputeScreenPos(o.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            half4 shadow_frag(v2f i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float shadow = GetShadowAABB(i.worldPos);
                //col.rgb *= (1-shadow);

                //float shadow = GetShadowSphere(i.worldPos);
                //col.rgb *= (1-shadow);


                col.rgb *= shadow;

                return col;
            }


            ENDHLSL
        }
    }
}