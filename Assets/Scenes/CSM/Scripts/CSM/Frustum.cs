using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Shadow.CSM
{
    /// <summary>
    ///  相机视野视锥体数据
    /// </summary>
    public class Frustum
    {

        public float m_near;
        public float m_far;

        // 远近平面四点 (左上开始顺时针)
        public Vector3[] m_nearPlaneWorldPoints;
        public Vector3[] m_farPlaneWorldPoints;



        public Matrix4x4 m_viewMt;          // 视野变换 -- 世界空间到本地空间
        public Matrix4x4 m_projMt;          // 投影变换

        public Camera m_debugCam;



        public void SetNearPlanePoints(Vector3[] points, Matrix4x4 localToWorldMT)
        {
            if (m_nearPlaneWorldPoints == null) m_nearPlaneWorldPoints = new Vector3[points.Length];

            for (int i = 0; i < points.Length; i++)
            {
                m_nearPlaneWorldPoints[i] = localToWorldMT.MultiplyPoint3x4(points[i]);
            }
        }

        public void SetFarPlanePoints(Vector3[] points, Matrix4x4 localToWorldMT)
        {
            if (m_farPlaneWorldPoints == null) m_farPlaneWorldPoints = new Vector3[points.Length];

            for (int i = 0; i < points.Length; i++)
            {
                m_farPlaneWorldPoints[i] = localToWorldMT.MultiplyPoint3x4(points[i]);
            }
        }

        public void SetNearFar(float near, float far)
        {
            m_near = near;
            m_far = far;
        }

        public void DebugDrawFrustum(Color clr, Color aabbClr)
        {
            int len = 4;
            for (int i = 0; i < len; i++)
            {
                Debug.DrawLine(m_nearPlaneWorldPoints[i], m_nearPlaneWorldPoints[(i + 1) % len], clr);
                Debug.DrawLine(m_farPlaneWorldPoints[i], m_farPlaneWorldPoints[(i + 1) % len], clr);
                Debug.DrawLine(m_nearPlaneWorldPoints[i], m_farPlaneWorldPoints[i], clr);
            }

            //for (int i = 0; i < len; i++)
            //{
            //    Debug.DrawLine(m_aabb[i], m_aabb[(i + 1) % len], aabbClr);
            //    Debug.DrawLine(m_aabb[i + 4], m_aabb[((i + 1) % len) + 4], aabbClr);
            //    Debug.DrawLine(m_aabb[i], m_aabb[i + 4], aabbClr);
            //}
        }



        #region AABB 包围盒

        public Vector3[] m_aabb;            // 包围盒世界坐标
        public float m_aabbZLen;            // 包围盒 Z轴长度
        public void UpdateAABB(Matrix4x4 worldToLightLocal)
        {
            // 光源空间下的 AABB包围盒
            var minXYZ = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var maxXYZ = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var p in m_nearPlaneWorldPoints)
            {
                var lightLocalPos = worldToLightLocal.MultiplyPoint3x4(p);
                minXYZ.x = Mathf.Min(lightLocalPos.x, minXYZ.x);
                minXYZ.y = Mathf.Min(lightLocalPos.y, minXYZ.y);
                minXYZ.z = Mathf.Min(lightLocalPos.z, minXYZ.z);
                maxXYZ.x = Mathf.Max(lightLocalPos.x, maxXYZ.x);
                maxXYZ.y = Mathf.Max(lightLocalPos.y, maxXYZ.y);
                maxXYZ.z = Mathf.Max(lightLocalPos.z, maxXYZ.z);
                //Debug.LogError($"near:    {p},   {lightLocalPos}");
            }
            foreach (var p in m_farPlaneWorldPoints)
            {
                var lightLocalPos = worldToLightLocal.MultiplyPoint3x4(p);
                minXYZ.x = Mathf.Min(lightLocalPos.x, minXYZ.x);
                minXYZ.y = Mathf.Min(lightLocalPos.y, minXYZ.y);
                minXYZ.z = Mathf.Min(lightLocalPos.z, minXYZ.z);
                maxXYZ.x = Mathf.Max(lightLocalPos.x, maxXYZ.x);
                maxXYZ.y = Mathf.Max(lightLocalPos.y, maxXYZ.y);
                maxXYZ.z = Mathf.Max(lightLocalPos.z, maxXYZ.z);
                //Debug.LogError($"far:    {p},   {lightLocalPos}");
            }

            //Debug.LogError($"{minXYZ},  {maxXYZ}");

            // 外扩,   保证横截面为 正方形
            //var dis = Mathf.Max(maxXYZ.x - minXYZ.x, maxXYZ.y - minXYZ.y);
            //maxXYZ.x = minXYZ.x + dis;
            //maxXYZ.y = minXYZ.y + dis;


            if (m_aabb == null) m_aabb = new Vector3[8];

            // 再变换到世界空间
            var lightToWorldMt = worldToLightLocal.inverse;
            m_aabb[0] = lightToWorldMt.MultiplyPoint3x4(new Vector3(minXYZ.x, maxXYZ.y, minXYZ.z));
            m_aabb[1] = lightToWorldMt.MultiplyPoint3x4(new Vector3(maxXYZ.x, maxXYZ.y, minXYZ.z));
            m_aabb[2] = lightToWorldMt.MultiplyPoint3x4(new Vector3(maxXYZ.x, minXYZ.y, minXYZ.z));
            m_aabb[3] = lightToWorldMt.MultiplyPoint3x4(new Vector3(minXYZ.x, minXYZ.y, minXYZ.z));
            m_aabb[4] = lightToWorldMt.MultiplyPoint3x4(new Vector3(minXYZ.x, maxXYZ.y, maxXYZ.z));
            m_aabb[5] = lightToWorldMt.MultiplyPoint3x4(new Vector3(maxXYZ.x, maxXYZ.y, maxXYZ.z));
            m_aabb[6] = lightToWorldMt.MultiplyPoint3x4(new Vector3(maxXYZ.x, minXYZ.y, maxXYZ.z));
            m_aabb[7] = lightToWorldMt.MultiplyPoint3x4(new Vector3(minXYZ.x, minXYZ.y, maxXYZ.z));


            m_aabbZLen = maxXYZ.z - minXYZ.z;
            float w = Vector3.Magnitude(m_aabb[1] - m_aabb[0]);
            float h = Vector3.Magnitude(m_aabb[1] - m_aabb[2]);
            var rotation = worldToLightLocal.rotation;
            rotation.Set(-rotation.x, -rotation.y, -rotation.z, rotation.w);
            m_viewMt = Matrix4x4.TRS((m_aabb[3] + m_aabb[1]) * 0.5f, rotation, Vector3.one).inverse;
            if (SystemInfo.usesReversedZBuffer) m_viewMt.SetRow(2, -1 * m_viewMt.GetRow(2));    // 第三行取反.  平台不同需要反转 Z(D3D近平面0, openGL近平面1)---C#(SystemInfo.usesReversedZBuffer), shader(UNITY_REVERSED_Z)
            m_projMt = Matrix4x4.Ortho(-w * 0.5f, w * 0.5f, -h * 0.5f, h * 0.5f, 0, m_aabbZLen);

            //if (m_debugCam != null)
            //{
            //    m_debugCam.transform.position = (m_aabb[3] + m_aabb[1]) * 0.5f;
            //    m_debugCam.orthographicSize = h * 0.5f;
            //    m_debugCam.nearClipPlane = 0;
            //    m_debugCam.farClipPlane = m_aabbZLen;
            //    m_debugCam.aspect = w / h;
            //}
        }
        public static Matrix4x4 CountProjMatrix(Vector3 min, Vector3 max, float near, float far)
        {
            var mt = new Matrix4x4();
            mt.m00 = 2.0f / (max.x - min.x);
            mt.m03 = -(max.x + min.x) / (max.x - min.x);
            mt.m11 = 2.0f / (max.y - min.y);
            mt.m13 = -(max.y + min.y) / (max.y - min.y);
            mt.m22 = -2.0f / (far - near);
            mt.m23 = -(far + near) / (far - near);
            mt.m33 = 1.0f;

            return mt;
        }

        #endregion



        #region 包围球--减少相机变动时 阴影抖动

        public Vector4 m_boundingSphere;        // Vector4(球心坐标, 球半径平方)
        public Vector3 m_boundingSphereCenter;
        public float m_boundingSphereR;

        /// <summary>
        ///  计算视锥体的包围球.  参数 UE 源码  https://blog.csdn.net/ZJU_fish1996/article/details/116401362   DirectionalLightComponent.cpp
        /// </summary>
        /// <returns></returns>
        public Vector4 BoundingSphere(Vector3 cameraPos, Vector3 cameraForward, Matrix4x4 cameraPorjMt)
        {
            //float aspectRatio = cameraPorjMt.m11 / cameraPorjMt.m00;
            float halfHorizontalFovTan = 1.0f / cameraPorjMt.m00;
            float halfVerticalFovTan = 1.0f / cameraPorjMt.m11;
            var Fl = m_far - m_near;

            var farX = halfHorizontalFovTan * m_far;
            var farY = halfVerticalFovTan * m_far;
            var Da = farX * farX + farY * farY;

            var nearX = halfHorizontalFovTan * m_near;
            var nearY = halfVerticalFovTan * m_near;
            var Db = nearX * nearX + nearY * nearY;

            var optimalOffset = (Db - Da) / (2.0f * Fl) + Fl * 0.5f;
            var centerZ = Mathf.Clamp(m_far - optimalOffset, m_near, m_far);
            //Debug.LogError($"{m_near},  {m_far},   {centerZ},  {optimalOffset},  {Db},  {Da}");

            m_boundingSphereCenter = cameraPos + cameraForward * centerZ;
            m_boundingSphereR = 0f;
            foreach (var corner in m_nearPlaneWorldPoints)
            {
                m_boundingSphereR = Mathf.Max(m_boundingSphereR, Vector3.Distance(m_boundingSphereCenter, corner));
            }
            foreach (var corner in m_farPlaneWorldPoints)
            {
                m_boundingSphereR = Mathf.Max(m_boundingSphereR, Vector3.Distance(m_boundingSphereCenter, corner));
            }

            return new Vector4(m_boundingSphereCenter.x, m_boundingSphereCenter.y, m_boundingSphereCenter.z, m_boundingSphereR * m_boundingSphereR);
        }

        public void UpdateSphereBounding(Vector3 cameraPos, Vector3 cameraForward, Quaternion lightRotation, Vector3 lightForward, Matrix4x4 cameraPorjMt)
        {
            m_boundingSphere = BoundingSphere(cameraPos, cameraForward, cameraPorjMt);

            var startPos = m_boundingSphereCenter - lightForward * m_boundingSphereR;       // 调整 光源正交视锥正好包括整个包围球

            var rotation = lightRotation;
            //rotation.Set(-rotation.x, -rotation.y, -rotation.z, rotation.w);
            m_viewMt = Matrix4x4.TRS(startPos, rotation, Vector3.one).inverse;
            if (SystemInfo.usesReversedZBuffer) m_viewMt.SetRow(2, -1 * m_viewMt.GetRow(2));    // 第三行取反.  平台不同需要反转 Z(D3D近平面0, openGL近平面1)---C#(SystemInfo.usesReversedZBuffer), shader(UNITY_REVERSED_Z)

            m_projMt = Matrix4x4.Ortho(-m_boundingSphereR, m_boundingSphereR, -m_boundingSphereR, m_boundingSphereR, 0, m_boundingSphereR * 2f);

            if (m_debugCam != null)
            {
                m_debugCam.transform.position = startPos;
                m_debugCam.transform.forward = lightForward;
                m_debugCam.orthographicSize = m_boundingSphereR;
                m_debugCam.nearClipPlane = 0;
                m_debugCam.farClipPlane = m_boundingSphereR * 2f;
                m_debugCam.aspect = 1;

                //Debug.LogError($"{m_viewMt},  {m_projMt}");
                //Debug.LogError($"{m_debugCam.transform.worldToLocalMatrix},  {m_debugCam.projectionMatrix}");
            }
        }

        #endregion
    }
}