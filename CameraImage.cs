using System;
using UnityEngine;

public class CameraImage : MonoBehaviour
{
    public Action<WebCamTexture> imageUpdate;
    WebCamTexture tex;
    
    private void OnEnable()
    {
        tex = new WebCamTexture();
        tex.requestedWidth = 960;
        tex.requestedHeight = 540;
        tex.deviceName = WebCamTexture.devices[0].name;
        GetComponent<Renderer>().material.SetTexture("_BaseMap", tex);
        tex.Play();
    }

    private void Update()
    {
        if (tex.didUpdateThisFrame) imageUpdate?.Invoke(tex);
    }
}
