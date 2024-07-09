using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class CameraEvents : MonoBehaviour
{
    public delegate void CameraEvent(Camera cam);

    [HideInInspector]
    public Camera cam;

    public event CameraEvent ce_OnPreCull;
    public event CameraEvent ce_OnPreRender;
    public event CameraEvent ce_OnPostRender;
    //public event CameraEvent ce_OnRenderImage;

    public static CameraEvents GetFromCamera(Camera cam)
    {
        CameraEvents ce = cam.GetComponent<CameraEvents>();
        if(ce == null)
        {
            ce = cam.gameObject.AddComponent<CameraEvents>();
        }
        return ce;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void OnPreCull()
    {
        ce_OnPreCull?.Invoke(cam);
    }

    private void OnPreRender()
    {
        ce_OnPreRender?.Invoke(cam);
    }

    private void OnPostRender()
    {
        ce_OnPostRender?.Invoke(cam);
    }
}
