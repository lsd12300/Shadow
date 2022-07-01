using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Shadow.CSM;

public class LogEditor
{


    [MenuItem("GameObject/LogViewMatrix", false, 10)]
    static void LogViewMatrix()
    {
        var obj = Selection.objects[0] as GameObject;
        var lightTran = obj.transform;
        Debug.LogError($"{lightTran.worldToLocalMatrix}");


        // 世界空间 到 光源空间 矩阵  === transform.worldToLocalMatrix
        var viewMt = Matrix4x4.identity;
        viewMt.SetTRS(lightTran.position, lightTran.rotation, Vector3.one);
        viewMt = viewMt.inverse;

        Debug.LogError($"{viewMt}");

        Debug.LogError($"{lightTran.rotation},  {lightTran.rotation.eulerAngles},  {viewMt.rotation},  {viewMt.rotation.eulerAngles}");
        //Debug.LogError($"{viewMt.GetRow(2)}"); ;

        //Debug.LogError($"{lightTran.worldToLocalMatrix.MultiplyPoint3x4(new Vector3(-2.31f, 20.83f, 2.87f))}");
    }


    [MenuItem("GameObject/LogProjMatrix", false, 10)]
    static void LogProjMatrix()
    {
        var obj = Selection.objects[0] as GameObject;

        Debug.LogError($"{obj.GetComponent<Camera>()?.projectionMatrix}");
        //Debug.LogError($"{GL.GetGPUProjectionMatrix(obj.GetComponent<Camera>().projectionMatrix, false)}");
        //Debug.LogError($"{obj.transform.worldToLocalMatrix}");

        //Matrix4x4 mt = new Matrix4x4(
        //    new Vector4(0.43814f, 0f, 0f, 0f),
        //    new Vector4(0f, -0.25553f, 0.04241f, 0f),
        //    new Vector4(0f, -0.09301f, -0.11652f, 0f),
        //    new Vector4(0f, 6.32167f, -0.18081f, 1f)
        //);

        ////var vPos = mt.MultiplyPoint(new Vector4(-0.74349f, 20.83f, 4.31978f, 1f));
        //var vPos = MatrixVec4(mt, new Vector4(-0.74349f, 20.83f, 4.31978f, 1f));
        //var viewPos = new Vector4(vPos.x, vPos.y, vPos.z, 1f);
        //var screenPos = ClipToScreenPos(viewPos);

        //Debug.LogError($"{viewPos},   {screenPos}");
    }

    [MenuItem("GameObject/LogTest", false, 10)]
    static void LogTest()
    {
        //int count = 4;
        //for (int i = 0; i < count; i++)
        //{
        //    Debug.LogError($"{CSM.ComputeCascadeSplit(2, i, count)}");
        //}

        var splits = CSM.ComputeAllCascadeSplits(2, 8);
        var val = 0f;
        foreach (var s in splits)
        {
            val += s;
            Debug.LogError($"{s}");
        }
        Debug.LogError($"{val}");
    }

    static Vector4 ClipToScreenPos(Vector4 clipPos)
    {
        var screenPos = Vector4.zero;

        screenPos.x = clipPos.x * 0.5f + clipPos.w * 0.5f;
        screenPos.y = -clipPos.y * 0.5f + clipPos.w * 0.5f;
        screenPos.z = clipPos.z;
        screenPos.w = clipPos.w;

        return screenPos;
    }

    static Vector4 MatrixVec4(Matrix4x4 mt, Vector4 point)
    {
        Vector4 result = default(Vector4);
        result.x = mt.m00 * point.x + mt.m01 * point.y + mt.m02 * point.z + mt.m03 * point.w;
        result.y = mt.m10 * point.x + mt.m11 * point.y + mt.m12 * point.z + mt.m13 * point.w;
        result.z = mt.m20 * point.x + mt.m21 * point.y + mt.m22 * point.z + mt.m23 * point.w;
        result.w = mt.m30 * point.x + mt.m31 * point.y + mt.m32 * point.z + mt.m33 * point.w;
        //num = 1f / num;
        //result.x *= num;
        //result.y *= num;
        //result.z *= num;
        return result;
    }


    //static Matrix4x4 mt1 = new Matrix4x4() {
    //    0.43814f, 0.00000f,  0.00000f,  0.00000f,
    //    0.00000f, 0.25612f,  0.00000f, -5.74723f,
    //    0.00000f, 0.00000f, -0.25502f, -1.00000f,
    //    0.00000f, 0.00000f,  0.00000f,  1.00000f,
    //};

    static Matrix4x4 mt1 = new Matrix4x4(new Vector4(0.43814f, 0, 0, 0), new Vector4(0, 0.25612f, 0, 0), new Vector4(0, 0, -0.25502f, 0), new Vector4(0, -5.74723f, -1, 1));
    static Matrix4x4 mt2 = new Matrix4x4(new Vector4(0.15378f, 0, 0, 0), new Vector4(0, 0.08989f, 0, 0), new Vector4(0, 0, -0.11936f, 0), new Vector4(0, -1.69924f, -1, 1));


}
