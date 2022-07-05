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

            if (m_debugAABB != null && m_debugAABB.Length >= 8)
            {
                for (int i = 0; i < len; i++)
                {
                    Debug.DrawLine(m_debugAABB[i], m_debugAABB[(i + 1) % len], aabbClr);
                    Debug.DrawLine(m_debugAABB[i + 4], m_debugAABB[((i + 1) % len) + 4], aabbClr);
                    Debug.DrawLine(m_debugAABB[i], m_debugAABB[i + 4], aabbClr);
                }
            }
        }



        #region AABB 包围盒

        public float m_aabbZLen;            // 包围盒 Z轴长度
        private Vector3 m_minXYZ;
        private Vector3 m_maxXYZ;
        public Vector3[] m_debugAABB;            // 包围盒世界坐标

        public void UpdateAABB(Matrix4x4 worldToLightLocal, Vector2Int shadowMapSize)
        {
            // 光源空间下的 AABB包围盒
            m_minXYZ = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            m_maxXYZ = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var p in m_nearPlaneWorldPoints)
            {
                var lightLocalPos = worldToLightLocal.MultiplyPoint3x4(p);
                m_minXYZ.x = Mathf.Min(lightLocalPos.x, m_minXYZ.x);
                m_minXYZ.y = Mathf.Min(lightLocalPos.y, m_minXYZ.y);
                m_minXYZ.z = Mathf.Min(lightLocalPos.z, m_minXYZ.z);
                m_maxXYZ.x = Mathf.Max(lightLocalPos.x, m_maxXYZ.x);
                m_maxXYZ.y = Mathf.Max(lightLocalPos.y, m_maxXYZ.y);
                m_maxXYZ.z = Mathf.Max(lightLocalPos.z, m_maxXYZ.z);
                //Debug.LogError($"near:    {p},   {lightLocalPos}");
            }
            foreach (var p in m_farPlaneWorldPoints)
            {
                var lightLocalPos = worldToLightLocal.MultiplyPoint3x4(p);
                m_minXYZ.x = Mathf.Min(lightLocalPos.x, m_minXYZ.x);
                m_minXYZ.y = Mathf.Min(lightLocalPos.y, m_minXYZ.y);
                m_minXYZ.z = Mathf.Min(lightLocalPos.z, m_minXYZ.z);
                m_maxXYZ.x = Mathf.Max(lightLocalPos.x, m_maxXYZ.x);
                m_maxXYZ.y = Mathf.Max(lightLocalPos.y, m_maxXYZ.y);
                m_maxXYZ.z = Mathf.Max(lightLocalPos.z, m_maxXYZ.z);
                //Debug.LogError($"far:    {p},   {lightLocalPos}");
            }

            // 消除相机移动时抖动 (https://zhuanlan.zhihu.com/p/116731971)
            //   要保证两帧之间像素完全重合.  固定相机在 世界长度和阴影贴图长度比 的整数倍的位置
            var maxDist = Mathf.Max(Vector3.Distance(m_nearPlaneWorldPoints[0], m_farPlaneWorldPoints[2]), Vector3.Distance(m_farPlaneWorldPoints[0], m_farPlaneWorldPoints[2]));
            var worldUnitsPerTexel = maxDist / shadowMapSize.x * 2f;

            var posX = (m_minXYZ.x + m_maxXYZ.x) * 0.5f;
            posX = Mathf.FloorToInt(posX / worldUnitsPerTexel) * worldUnitsPerTexel;

            var posY = (m_minXYZ.y + m_maxXYZ.y) * 0.5f;
            posY = Mathf.FloorToInt(posY / worldUnitsPerTexel) * worldUnitsPerTexel;


            var center = worldToLightLocal.inverse.MultiplyPoint3x4(new Vector3(posX, posY, m_minXYZ.z));
            m_aabbZLen = m_maxXYZ.z - m_minXYZ.z;
            var rotation = worldToLightLocal.rotation;
            rotation.Set(-rotation.x, -rotation.y, -rotation.z, rotation.w);
            m_viewMt = Matrix4x4.TRS(center, rotation, Vector3.one).inverse;
            if (SystemInfo.usesReversedZBuffer) m_viewMt.SetRow(2, -1 * m_viewMt.GetRow(2));    // 第三行取反.  平台不同需要反转 Z(D3D近平面0, openGL近平面1)---C#(SystemInfo.usesReversedZBuffer), shader(UNITY_REVERSED_Z)
            m_projMt = Matrix4x4.Ortho(-maxDist * 0.5f, maxDist * 0.5f, -maxDist * 0.5f, maxDist * 0.5f, 0, m_aabbZLen);

            //Debug.LogError($"{maxDist}");

            if (m_debugCam != null)
            {
                m_debugCam.transform.position = center;
                m_debugCam.orthographicSize = maxDist * 0.5f;
                m_debugCam.nearClipPlane = 0;
                m_debugCam.farClipPlane = m_aabbZLen;
                m_debugCam.aspect = 1;


                if (m_debugAABB == null) m_debugAABB = new Vector3[8];
                var lightToWorldMt = worldToLightLocal.inverse;
                m_debugAABB[0] = lightToWorldMt.MultiplyPoint3x4(new Vector3(posX - maxDist * 0.5f, posY - maxDist * 0.5f, m_minXYZ.z));
                m_debugAABB[1] = lightToWorldMt.MultiplyPoint3x4(new Vector3(posX - maxDist * 0.5f, posY + maxDist * 0.5f, m_minXYZ.z));
                m_debugAABB[2] = lightToWorldMt.MultiplyPoint3x4(new Vector3(posX + maxDist * 0.5f, posY + maxDist * 0.5f, m_minXYZ.z));
                m_debugAABB[3] = lightToWorldMt.MultiplyPoint3x4(new Vector3(posX + maxDist * 0.5f, posY - maxDist * 0.5f, m_minXYZ.z));
                m_debugAABB[4] = lightToWorldMt.MultiplyPoint3x4(new Vector3(posX - maxDist * 0.5f, posY - maxDist * 0.5f, m_maxXYZ.z));
                m_debugAABB[5] = lightToWorldMt.MultiplyPoint3x4(new Vector3(posX - maxDist * 0.5f, posY + maxDist * 0.5f, m_maxXYZ.z));
                m_debugAABB[6] = lightToWorldMt.MultiplyPoint3x4(new Vector3(posX + maxDist * 0.5f, posY + maxDist * 0.5f, m_maxXYZ.z));
                m_debugAABB[7] = lightToWorldMt.MultiplyPoint3x4(new Vector3(posX + maxDist * 0.5f, posY - maxDist * 0.5f, m_maxXYZ.z));
            }
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
        public Vector4 BoundingSphere(Vector3 cameraPos, Vector3 cameraForward, Matrix4x4 cameraPorjMt, Vector2Int shadowMapSize)
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


        /// <summary>
        ///   克莱姆法则  空间四点确定球心
        /// </summary>
        public Vector4 BoundingSphere(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
        {
            m_boundingSphereCenter = CramerRuleSphere(p1.x, p1.y, p1.z, p2.x, p2.y, p2.z, p3.x, p3.y, p3.z, p4.x, p4.y, p4.z);
            m_boundingSphereR = Vector3.Distance(m_boundingSphereCenter, p1);
            return new Vector4(m_boundingSphereCenter.x, m_boundingSphereCenter.y, m_boundingSphereCenter.z, m_boundingSphereR * m_boundingSphereR);
        }

        public Vector3 CramerRuleSphere(
            float x1, float y1, float z1,
            float x2, float y2, float z2,
            float x3, float y3, float z3,
            float x4, float y4, float z4)
        {
            float a11, a12, a13, a21, a22, a23, a31, a32, a33, b1, b2, b3, d, d1, d2, d3;
            a11 = 2 * (x2 - x1); a12 = 2 * (y2 - y1); a13 = 2 * (z2 - z1);
            a21 = 2 * (x3 - x2); a22 = 2 * (y3 - y2); a23 = 2 * (z3 - z2);
            a31 = 2 * (x4 - x3); a32 = 2 * (y4 - y3); a33 = 2 * (z4 - z3);
            b1 = x2 * x2 - x1 * x1 + y2 * y2 - y1 * y1 + z2 * z2 - z1 * z1;
            b2 = x3 * x3 - x2 * x2 + y3 * y3 - y2 * y2 + z3 * z3 - z2 * z2;
            b3 = x4 * x4 - x3 * x3 + y4 * y4 - y3 * y3 + z4 * z4 - z3 * z3;
            d = a11 * a22 * a33 + a12 * a23 * a31 + a13 * a21 * a32 - a11 * a23 * a32 - a12 * a21 * a33 - a13 * a22 * a31;
            d1 = b1 * a22 * a33 + a12 * a23 * b3 + a13 * b2 * a32 - b1 * a23 * a32 - a12 * b2 * a33 - a13 * a22 * b3;
            d2 = a11 * b2 * a33 + b1 * a23 * a31 + a13 * a21 * b3 - a11 * a23 * b3 - b1 * a21 * a33 - a13 * b2 * a31;
            d3 = a11 * a22 * b3 + a12 * b2 * a31 + b1 * a21 * a32 - a11 * b2 * a32 - a12 * a21 * b3 - b1 * a22 * a31;
            float x = d1 / d;
            float y = d2 / d;
            float z = d3 / d;
            return new Vector3(x, y, z);
        }




        public void UpdateSphereBounding(Vector3 cameraPos, Vector3 cameraForward, 
            Quaternion lightRotation, Vector3 lightForward, Matrix4x4 cameraPorjMt, Vector2Int shadowMapSize, Matrix4x4 worldToLightMT)
        {
            //m_boundingSphere = BoundingSphere(cameraPos, cameraForward, cameraPorjMt, shadowMapSize);
            m_boundingSphere = BoundingSphere(m_nearPlaneWorldPoints[0], m_farPlaneWorldPoints[0], m_farPlaneWorldPoints[1], m_farPlaneWorldPoints[2]);


            // 消除相机移动时抖动 (https://zhuanlan.zhihu.com/p/116731971)
            //   要保证两帧之间像素完全重合.  固定相机在 世界长度和阴影贴图长度比 的整数倍的位置
            var worldUnitsPerTexel = m_boundingSphereR * 2f / shadowMapSize.x * 2f;
            var lightSpaceCenter = worldToLightMT.MultiplyPoint3x4(m_boundingSphereCenter);

            var posX = lightSpaceCenter.x;
            posX /= worldUnitsPerTexel;
            posX = Mathf.FloorToInt(posX);
            posX *= worldUnitsPerTexel;

            var posY = lightSpaceCenter.y;
            posY /= worldUnitsPerTexel;
            posY = Mathf.FloorToInt(posY);
            posY *= worldUnitsPerTexel;

            m_boundingSphereCenter = worldToLightMT.inverse.MultiplyPoint3x4(new Vector3(posX, posY, lightSpaceCenter.z));
            m_boundingSphere = new Vector4(m_boundingSphereCenter.x, m_boundingSphereCenter.y, m_boundingSphereCenter.z, m_boundingSphereR * m_boundingSphereR);


            var startPos = m_boundingSphereCenter - lightForward * m_boundingSphereR;       // 调整 光源正交视锥正好包括整个包围球

            var rotation = lightRotation;
            //rotation.Set(-rotation.x, -rotation.y, -rotation.z, rotation.w);
            m_viewMt = Matrix4x4.TRS(startPos, rotation, Vector3.one).inverse;
            if (SystemInfo.usesReversedZBuffer) m_viewMt.SetRow(2, -1 * m_viewMt.GetRow(2));    // 第三行取反.  平台不同需要反转 Z(D3D近平面0, openGL近平面1)---C#(SystemInfo.usesReversedZBuffer), shader(UNITY_REVERSED_Z)

            m_projMt = Matrix4x4.Ortho(-m_boundingSphereR, m_boundingSphereR, -m_boundingSphereR, m_boundingSphereR, 0, m_boundingSphereR * 2f);

            //Debug.LogError($"{m_boundingSphereR * 2.0f}");

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