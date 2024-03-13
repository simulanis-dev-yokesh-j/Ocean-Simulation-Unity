using System;
using UnityEngine;
using UnityEngine.Rendering;

public class OceanCpuData : MonoBehaviour
{
    public event Action<Texture2D> OnHeightMapReceived;
    private bool _requested;
    
    private RenderTexture _heightMap;
    private Texture2D _heightMapTexture;
    private OceanGenerator _oceanGenerator;

    private void Awake()
    {
        _oceanGenerator = GetComponent<OceanGenerator>();
    }

    private void Start()
    {
        _heightMap = _oceanGenerator.GetCascadeHeightsMap();
        _heightMapTexture = new Texture2D(_heightMap.width, _heightMap.height, TextureFormat.RGBAHalf, false);
        _heightMapTexture.wrapMode = TextureWrapMode.Repeat;
        
        _oceanGenerator.OnRuntimeUpdate += OnRuntimeUpdate;
    }

    private void Update()
    {
        RequestWaterHeight();
    }
    
    private void OnRuntimeUpdate()
    {
        _heightMap = _oceanGenerator.GetCascadeHeightsMap();
    }

    private void RequestWaterHeight()
    {
        if(_requested)
            return;
        
        _requested = true;
        AsyncGPUReadback.Request(_heightMap, 0, 0, _heightMap.width, 0, _heightMap.height, 0, 1, TextureFormat.RGBAHalf, OnCompleteReadback);
    }
    
    private void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.Log("Failed to read GPU texture");
            return;
        }

        var data = request.GetData<byte>();
        
        _heightMapTexture.LoadRawTextureData(data);
        _heightMapTexture.Apply();
        OnHeightMapReceived?.Invoke(_heightMapTexture);
        
        _requested = false;
    }

    private void OnDestroy()
    {
        _oceanGenerator.OnRuntimeUpdate -= OnRuntimeUpdate;
    }
}
