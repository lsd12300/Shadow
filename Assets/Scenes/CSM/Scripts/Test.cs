using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Shadow.CSM;


public class Test : MonoBehaviour
{
    private CSM m_csm;
    private Camera m_cam;


    private void Start()
    {
        //var light = GameObject.Find("Directional Light").GetComponent<Light>();
        //m_cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        //m_csm = new CSM(light, m_cam);

        //m_csm.m_debugCam = GameObject.Find("DebugCamera").GetComponent<Camera>();

    }


    //void Update()
    //{
    //    m_csm.UpdateCSM();

    //    m_csm.DebugDrawFrustum();
    //}
}
