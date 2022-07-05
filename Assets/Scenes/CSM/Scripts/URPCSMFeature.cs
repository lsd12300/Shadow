using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Shadow.CSM;
using System;


public class URPCSMFeature : ScriptableRendererFeature
{

    public enum CascadeCount
    {
        One = 1,
        Four = 4,
        Eight = 8,
    }

    /// <summary>
    ///  视野深度划分类型
    /// </summary>
    public enum CascadeSplitType
    {
        Average = 1,        // 均分
        Double = 2,         // 每级是上一级的2倍
    }


    [Serializable]
    public class RenderPassSettings
    {
        public RenderPassEvent renderEvt;

        public string shaderTag;

        public int perLevelTextureSize;

        public CascadeCount cascadeCount;

        public CascadeSplitType cascadeSplitType;
    }


    class CustomRenderPass : ScriptableRenderPass
    {

        private CSM m_csm;

        private int m_perLevelTextureSize;
        private ShaderTagId m_shaderTagID;
        private int m_cascadeCount;
        private int m_cascadeSplitType;


        private Vector2Int m_shadowTexSize;
        private float m_perLevelTextureSizeR;       // 1.0 / m_perLevelTextureSize


        //private ShaderTagId m_shaderTagID = new ShaderTagId("UniversalForward");
        private FilteringSettings m_FilteringSettings;
        private string m_profilerTag = "CSM";
        private ProfilingSampler m_ProfilingSampler;

        private RenderTargetHandle m_depthRT;
        private RenderTargetIdentifier m_colorTarget;

        private Matrix4x4[] m_LightSpaceVs;
        private Matrix4x4[] m_LightSpacePs;
        private Matrix4x4[] m_LightSpaceVPs;
        private float[] m_LightSpaceZLens;
        private Vector4[] m_LightBoundingSphere;
        private Matrix4x4[] m_ShadowTexUVMatrixs;
        public const string CascadeShadowTexName = "_CascadeShadowTex";
        private static int m_CascadeShadowTexProp = Shader.PropertyToID(CascadeShadowTexName);
        private static int m_CascadeCountProp = Shader.PropertyToID("_CascadeCount");
        private static int m_LightSpaceCascadeVsProp = Shader.PropertyToID("_LightSpaceCascadeVs");       // 光源空间各级阴影视野的 V矩阵
        private static int m_LightSpaceCascadePsProp = Shader.PropertyToID("_LightSpaceCascadePs");       // 光源空间各级阴影视野的 P矩阵
        private static int m_LightSpaceCascadeVPsProp = Shader.PropertyToID("_LightSpaceCascadeVPs");       // 光源空间各级阴影视野的 VP矩阵
        private static int m_CascadeSplitDisesProp = Shader.PropertyToID("_CascadeSplitDises");
        private static int m_LightSpaceZLensProp = Shader.PropertyToID("_LightSpaceZLens");
        private static int m_LightBoundingSphereProp = Shader.PropertyToID("_LightBoundingSphere");
        private static int m_ShadowTexUVMatrixProp = Shader.PropertyToID("_ShadowTexUVMatrix");

        private int[] m_cascadePreRenderFrameCount;         // 每级阴影上次渲染的帧---原神阴影方案(共8级, 前四级每帧更新, 后四级每帧仅更新一级)

        private static RenderTexture m_rt;


        public CustomRenderPass(RenderPassSettings settings)
        {
            renderPassEvent = settings.renderEvt;
            m_shaderTagID = new ShaderTagId(settings.shaderTag);
            m_perLevelTextureSize = settings.perLevelTextureSize;
            m_cascadeCount = (int)settings.cascadeCount;
            m_cascadeSplitType = (int)settings.cascadeSplitType;

            m_shadowTexSize = new Vector2Int(m_perLevelTextureSize * m_cascadeCount / 2, m_perLevelTextureSize * 2);
            m_perLevelTextureSizeR = 1.0f / m_perLevelTextureSize;


            if (m_csm == null)
            {
                m_csm = new CSM(GameObject.Find("Directional Light").GetComponent<Light>(), GameObject.Find("Main Camera").GetComponent<Camera>(),
                    m_cascadeCount, m_cascadeSplitType, m_shadowTexSize);
                m_csm.m_debugCam = GameObject.Find("DebugCamera").GetComponent<Camera>();
            }

            m_ProfilingSampler = new ProfilingSampler(m_profilerTag);
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque);

            //m_depthRT.Init(CascadeShadowTexName);

            if (m_rt == null)
            {
                m_rt = RenderTexture.GetTemporary(m_shadowTexSize.x, m_shadowTexSize.y, 32, RenderTextureFormat.Shadowmap);
                //m_rt = RenderTexture.GetTemporary(m_shadowTexSize.x, m_shadowTexSize.y, 0, RenderTextureFormat.BGRA32);
                m_rt.filterMode = FilterMode.Bilinear;
            }

            m_LightSpaceVs = new Matrix4x4[m_cascadeCount];
            m_LightSpacePs = new Matrix4x4[m_cascadeCount];
            m_LightSpaceVPs = new Matrix4x4[m_cascadeCount];
            m_LightSpaceZLens = new float[m_cascadeCount];
            m_LightBoundingSphere = new Vector4[m_cascadeCount];
            m_cascadePreRenderFrameCount = new int[m_cascadeCount];
            for (int i = 0; i < m_cascadeCount; i++)
            {
                m_cascadePreRenderFrameCount[i] = -1;
            }
            m_ShadowTexUVMatrixs = new Matrix4x4[m_cascadeCount];
        }


        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            //cmd.GetTemporaryRT(m_depthRT.id, m_perLevelTextureSize * m_cascadeCount / 2, m_perLevelTextureSize * 2, 32, UnityEngine.FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }

        public void Setup(RenderTargetIdentifier target)
        {
            m_colorTarget = target;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            m_csm?.UpdateCSM();     // 更新阴影级联
            //m_csm.DebugDrawFrustum();

            var drawSettings = CreateDrawingSettings(m_shaderTagID, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);

            var camData = renderingData.cameraData;
            //Debug.LogError($"{Time.frameCount},  {Time.renderedFrameCount}");

            var cmd = CommandBufferPool.Get(m_profilerTag);
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                // 设置渲染到贴图上
                cmd.SetRenderTarget(m_rt, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                //cmd.SetRenderTarget(m_depthRT.Identifier(), RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                cmd.ClearRenderTarget(true, true, Color.clear);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var fs = m_csm.Frustums;
                var horizontalCount = m_cascadeCount / 2;       // 阴影贴图中 横向阴影级数数量
                var frameCount = Time.frameCount;
                for (int i = 0; i < fs.Length; i++)
                {
                    //cmd.SetViewport(new Rect((i % horizontalCount) * m_perLevelTextureSize, (i / horizontalCount) * m_perLevelTextureSize,
                    //    m_perLevelTextureSize, m_perLevelTextureSize));        // 设置填充区域

                    Rect curCascadeRect = new Rect((i % horizontalCount) * m_perLevelTextureSize, (i / horizontalCount) * m_perLevelTextureSize,
                        m_perLevelTextureSize, m_perLevelTextureSize);

                    //使用之前的深度信息
                    //if (frameCount - m_cascadePreRenderFrameCount[i] <= 20) continue;          // 延迟更新深度信息
                    //else
                    {
                        // 设置阴影填充区域
                        if (m_cascadeCount > 1)      // 默认填充整张图 不需要调整
                        {
                            cmd.SetViewport(curCascadeRect);        // 设置填充区域
                        }
                        //cmd.ClearRenderTarget(true, true, Color.clear);         // 清除之前深度信息
                    }

                    var projMt = GL.GetGPUProjectionMatrix(fs[i].m_projMt, camData.IsCameraProjectionMatrixFlipped());
                    RenderingUtils.SetViewAndProjectionMatrices(cmd, fs[i].m_viewMt, projMt, false);    // 设置为 光源视野
                    //if (SystemInfo.usesReversedZBuffer) fs[i].m_viewMt.SetRow(2, -1 * fs[i].m_viewMt.GetRow(2));
                    m_LightSpaceVs[i] = fs[i].m_viewMt;
                    m_LightSpacePs[i] = projMt;
                    var areaUVMT = Matrix4x4.TRS(new Vector3(curCascadeRect.x / m_shadowTexSize.x, curCascadeRect.y/ m_shadowTexSize.y, 0), 
                        Quaternion.identity, 
                        new Vector3(m_perLevelTextureSizeR, m_perLevelTextureSizeR, 1));      // 屏幕空间变换到 阴影区域
                    m_LightSpaceVPs[i] = projMt * fs[i].m_viewMt;
                    m_LightSpaceZLens[i] = fs[i].m_aabbZLen;
                    m_LightBoundingSphere[i] = fs[i].m_boundingSphere;
                    m_ShadowTexUVMatrixs[i] = areaUVMT;

                    //// 设置阴影填充区域
                    //if(m_cascadeCount > 1)      // 默认填充整张图 不需要调整
                    //{
                    //    cmd.SetViewport(curCascadeRect);        // 设置填充区域
                    //}

                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings);
                    m_cascadePreRenderFrameCount[i] = frameCount;
                }

                cmd.SetGlobalFloat(m_CascadeCountProp, m_cascadeCount);
                cmd.SetGlobalTexture(m_CascadeShadowTexProp, m_rt);
                //cmd.SetGlobalTexture(m_CascadeShadowTexProp, m_depthRT.Identifier());
                cmd.SetGlobalMatrixArray(m_LightSpaceCascadeVsProp, m_LightSpaceVs);
                cmd.SetGlobalMatrixArray(m_LightSpaceCascadePsProp, m_LightSpacePs);
                cmd.SetGlobalMatrixArray(m_LightSpaceCascadeVPsProp, m_LightSpaceVPs);
                cmd.SetGlobalFloatArray(m_CascadeSplitDisesProp, m_csm.FrustumSplitDises);
                cmd.SetGlobalFloatArray(m_LightSpaceZLensProp, m_LightSpaceZLens);
                cmd.SetGlobalVectorArray(m_LightBoundingSphereProp, m_LightBoundingSphere);
                cmd.SetGlobalMatrixArray(m_ShadowTexUVMatrixProp, m_ShadowTexUVMatrixs);
            }

            cmd.SetRenderTarget(m_colorTarget);

            RenderingUtils.SetViewAndProjectionMatrices(cmd, camData.GetViewMatrix(), camData.GetGPUProjectionMatrix(), false); // 还原相机属性

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            //cmd.ReleaseTemporaryRT(m_depthRT.id);
        }
    }

    CustomRenderPass m_ScriptablePass;
    public RenderPassSettings m_renderSettings;

    public override void Create()
    {
        m_ScriptablePass = new CustomRenderPass(m_renderSettings);
    }


    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        m_ScriptablePass.Setup(renderer.cameraColorTarget);

        renderer.EnqueuePass(m_ScriptablePass);
    }
}
