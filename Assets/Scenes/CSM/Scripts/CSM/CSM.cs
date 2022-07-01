using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Shadow.CSM
{
    /// <summary>
    ///  级联阴影
    /// </summary>
    public class CSM
    {

        #region 属性

        public int CascadeCount { get; set; } = 1;      // 视野划分数量
        public int CascadeSplitType { get; set; } = 2;

        private float[] m_frustumPercents = { 0.07f, 0.13f, 0.25f, 0.55f };     // 视锥体分割参数.   Unity 默认为 {0.067f, 0.133f, 0.267f, 0.533f}
        private float[] m_frustumSplitDises;

        private Light m_light;
        private Camera m_camera;
        private Frustum[] m_frustums;

        public Camera m_debugCam;

        #endregion


        #region 方法

        public CSM(Light l, Camera cam, int cascadeCount, int cascadeSpliteType)
        {
            m_light = l;
            Cam = cam;

            CascadeCount = cascadeCount;
            CascadeSplitType = cascadeSpliteType;
            m_frustumSplitDises = new float[CascadeCount];
            m_frustums = new Frustum[CascadeCount];
            m_frustumPercents = ComputeAllCascadeSplits(CascadeSplitType, CascadeCount);
        }

        public Camera Cam { get => m_camera; set => m_camera = value; }

        public Frustum[] Frustums { get => m_frustums; }

        public float[] FrustumSplitDises { get => m_frustumSplitDises; }

        public void UpdateCSM()
        {
            // 1. 更新相机视野划分
            UpdateCameraFrustum();
        }

        /// <summary>
        ///  更新各级视野划分
        /// </summary>
        private void UpdateCameraFrustum()
        {
            if (m_camera == null) return;

            if (m_debugCam != null) m_debugCam.transform.rotation = m_light.transform.rotation;

            float dis = m_camera.farClipPlane - m_camera.nearClipPlane;
            float near = m_camera.nearClipPlane;
            float far = near + dis * m_frustumPercents[0];
            Vector3[] frustumFourCorners = new Vector3[4];
            var camLocal2World = m_camera.transform.localToWorldMatrix;
            for (int i = 0; i < CascadeCount; i++)
            {
                m_frustumSplitDises[i] = far;
                //Debug.LogError($"{i},  {m_frustumSplitDises[i]}");

                // 1. 远近平面数据
                if (m_frustums[i] == null) m_frustums[i] = new Frustum();
                m_frustums[i].SetNearFar(near, far);

                if (i <= 0)
                {
                    m_camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), near, Camera.MonoOrStereoscopicEye.Mono, frustumFourCorners);
                    m_frustums[i].SetNearPlanePoints(frustumFourCorners, camLocal2World);

                    m_frustums[i].m_debugCam = m_debugCam;
                }
                else
                {
                    m_frustums[i].SetNearPlanePoints(m_frustums[i-1].m_farPlaneWorldPoints, Matrix4x4.identity);
                }

                m_camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), far, Camera.MonoOrStereoscopicEye.Mono, frustumFourCorners);
                m_frustums[i].SetFarPlanePoints(frustumFourCorners, camLocal2World);

                //Debug.LogError($"{i},  {frustumFourCorners[0]},  {frustumFourCorners[2]}");

                // 2. AABB包围盒
                //m_frustums[i].UpdateAABB(m_light.transform.worldToLocalMatrix);

                // 2. 包围球
                m_frustums[i].UpdateSphereBounding(m_camera.transform.position, m_camera.transform.forward, m_light.transform.rotation, m_light.transform.forward, m_camera.projectionMatrix);
                //if (m_debugCam != null && m_debugCam.transform.childCount >= 4)
                //{
                //    var sphere = m_debugCam.transform.GetChild(i);
                //    sphere.position = m_frustums[i].m_boundingSphereCenter;
                //    sphere.localScale = 2.0f * m_frustums[i].m_boundingSphereR * Vector3.one;
                //}

                near = far;
                if (i < CascadeCount-1) far = near + dis * m_frustumPercents[i+1];
            }
        }

        #endregion


        #region 静态方法

        /// <summary>
        ///  计算级联视锥分割系数.  参考 UE 源码  DirectionalLightComponent.cpp    ComputeAccumulatedScale()
        /// </summary>
        /// <param name="exponent">分割类型:  1-线性分割,  2-每级放大2倍</param>
        /// <param name="cascadeIndex"></param>
        /// <param name="cascadeCount"></param>
        /// <returns></returns>
        public static float ComputeCascadeSplit(float exponent, int cascadeIndex, int cascadeCount)
        {
            if (cascadeIndex <= 0) return 0f;

            var curScale = 1f;
            var totleScale = 0f;
            var ret = 0f;

            for (int i = 0; i < cascadeCount; i++)
            {
                if (i < cascadeIndex) ret += curScale;

                totleScale += curScale;         // 累计所有视野比重
                curScale *= exponent;           // 每级视野比重
            }

            return ret / totleScale;
        }

        /// <summary>
        ///   计算全部视野划分, 数组元素--当前等级视野长度/总视野长度
        /// </summary>
        /// <param name="exponent">分割类型:  1-线性分割,  2-每级放大2倍</param>
        /// <param name="cascadeCount"></param>
        /// <returns></returns>
        public static float[] ComputeAllCascadeSplits(float exponent, int cascadeCount)
        {
            float[] splits = new float[cascadeCount];
            var curScale = 1f;
            var totleScale = 0f;

            for (int i = 0; i < cascadeCount; i++)
            {
                splits[i] = curScale;

                totleScale += curScale;         // 累计所有视野比重
                curScale *= exponent;           // 每级视野比重
            }

            for (int i = 0; i < cascadeCount; i++)
            {
                splits[i] /= totleScale;
            }


            return splits;
        }

        #endregion


        #region 调试

        public void DebugDrawFrustum()
        {
            //var clrs = new Color[] { Color.white, Color.white, Color.white, Color.white };
            var aabbClrs = new Color[] { Color.red, Color.green, Color.blue, Color.yellow };
            var clrs = new Color[] { Color.red, Color.green, Color.blue, Color.yellow };
            for (int i = 0; i < m_frustums.Length; i++)
            //for (int i = 0; i < 1; i++)
            {
                m_frustums[i].DebugDrawFrustum(clrs[i], aabbClrs[i]);
            }
        }

        #endregion
    }
}
