using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ComputeCustomFFT : MonoBehaviour
{
    public ComputeShader _butterflyComputeShader;
    public RawImage _buterflyTextureImage;
    
    private int _buterflyComputeKernel;
    private RenderTexture _butterflyTexture;
    
    private void Start()
    {
        int size = 128;
        int log = (int)Mathf.Log(size, 2);
        _butterflyTexture = CreateRenderTexture(log, size, RenderTextureFormat.ARGB64);
        _buterflyComputeKernel = _butterflyComputeShader.FindKernel("ComputeButterflyTexture");
        
        _butterflyComputeShader.SetInt("Size", size);
        _butterflyComputeShader.SetInt("LogSize", log);
        _butterflyComputeShader.SetTexture(_buterflyComputeKernel, "ButterflyTexture", _butterflyTexture);
        
        _butterflyComputeShader.Dispatch(_buterflyComputeKernel, log, size/8, 1);
        
        _buterflyTextureImage.texture = _butterflyTexture;
    }
    
    public RenderTexture CreateRenderTexture(int width, int height, RenderTextureFormat format = RenderTextureFormat.RGFloat, bool useMips = false)
    {
        RenderTexture rt = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear);
        //rt.useMipMap = useMips;
        //rt.autoGenerateMips = false;
        //rt.anisoLevel = 6;
        rt.filterMode = FilterMode.Point;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }
}
