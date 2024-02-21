using System;
using UnityEngine;

public class CameraImage : MonoBehaviour
{
    public Action<WebCamTexture> imageUpdate;
    private WebCamTexture _tex;

    public int Width = 960;
    public int Height = 540;

    private void OnEnable()
    {
        _tex = new WebCamTexture
        {
            requestedWidth = Width,
            requestedHeight = Height,
            deviceName = WebCamTexture.devices[0].name
        };
        var mat = GetComponent<Renderer>().material;
        if (mat != null)
        {
            if (mat.HasTexture("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", _tex);
            }
            else if (mat.HasTexture("_MainTex"))
            {
                mat.SetTexture("_MainTex", _tex);
            }
        }
        _tex.Play();
    }

    private void Update()
    {
        if (_tex.didUpdateThisFrame)
        {
            imageUpdate?.Invoke(_tex);
        }
    }
}
