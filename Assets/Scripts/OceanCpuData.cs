using System;
using UnityEngine;
using UnityEngine.Rendering;

public class OceanCpuData : MonoBehaviour
{
    public event Action<Texture2D, Texture2D, Texture2D> OnDisplacementsReceived;
    
    private bool _requested1;
    private bool _requested2;
    private bool _requested3;
    
    private RenderTexture _displacementMap1;
    private RenderTexture _displacementMap2;
    private RenderTexture _displacementMap3;
    private Texture2D _displacement1MapTexture;
    private Texture2D _displacement2MapTexture;
    private Texture2D _displacement3MapTexture;
    private OceanGenerator _oceanGenerator;

    private void Awake()
    {
        _oceanGenerator = GetComponent<OceanGenerator>();
    }

    private void Start()
    {
        _displacementMap1 = _oceanGenerator.GetWaveCascade1().GetSpectrumWrapperData().DisplacementMap;
        _displacement1MapTexture = new Texture2D(_displacementMap1.width, _displacementMap1.height, TextureFormat.RGBAHalf, false);
        _displacement1MapTexture.wrapMode = TextureWrapMode.Repeat;
        
        _displacementMap2 = _oceanGenerator.GetWaveCascade2().GetSpectrumWrapperData().DisplacementMap;
        _displacement2MapTexture = new Texture2D(_displacementMap2.width, _displacementMap2.height, TextureFormat.RGBAHalf, false);
        _displacement2MapTexture.wrapMode = TextureWrapMode.Repeat;
        
        _displacementMap3 = _oceanGenerator.GetWaveCascade3().GetSpectrumWrapperData().DisplacementMap;
        _displacement3MapTexture = new Texture2D(_displacementMap3.width, _displacementMap3.height, TextureFormat.RGBAHalf, false);
        _displacement3MapTexture.wrapMode = TextureWrapMode.Repeat;
        
        //_oceanGenerator.OnRuntimeUpdate += OnRuntimeUpdate;
    }

    private void Update()
    {
        RequestWaterHeight();
    }
    
    // private void OnRuntimeUpdate()
    // {
    //     _displacementMap1 = _oceanGenerator.GetCascadeHeightsMap();
    // }

    private void RequestWaterHeight()
    {
        if(_requested1 || _requested2 || _requested3)
            return;
        
        _requested1 = true;
        _requested2 = true;
        _requested3 = true;

        AsyncGPUReadback.Request(_displacementMap1, 0, 0, _displacementMap1.width, 0, _displacementMap1.height, 0, 1, TextureFormat.RGBAHalf, OnCompleteReadback1);
        AsyncGPUReadback.Request(_displacementMap2, 0, 0, _displacementMap2.width, 0, _displacementMap2.height, 0, 1, TextureFormat.RGBAHalf, OnCompleteReadback2);
        AsyncGPUReadback.Request(_displacementMap3, 0, 0, _displacementMap3.width, 0, _displacementMap3.height, 0, 1, TextureFormat.RGBAHalf, OnCompleteReadback3);
    }
    
    private void OnCompleteReadback1(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.Log("Failed to read GPU texture");
            return;
        }

        var data = request.GetData<byte>();
        
        _displacement1MapTexture.LoadRawTextureData(data);
        _displacement1MapTexture.Apply();

        _requested1 = false;
        FireDisplacementEvent();
    }
    
    private void OnCompleteReadback2(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.Log("Failed to read GPU texture");
            return;
        }

        var data = request.GetData<byte>();
        
        _displacement2MapTexture.LoadRawTextureData(data);
        _displacement2MapTexture.Apply();

        _requested2 = false;
        FireDisplacementEvent();
    }
    
    private void OnCompleteReadback3(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.Log("Failed to read GPU texture");
            return;
        }

        var data = request.GetData<byte>();
        
        _displacement3MapTexture.LoadRawTextureData(data);
        _displacement3MapTexture.Apply();

        _requested3 = false;
        FireDisplacementEvent();
    }

    private void FireDisplacementEvent()
    {
        if(_requested1 || _requested2 || _requested3)
            return;
        
        OnDisplacementsReceived?.Invoke(_displacement1MapTexture, _displacement2MapTexture, _displacement3MapTexture);
    }

    private void OnDestroy()
    {
        //_oceanGenerator.OnRuntimeUpdate -= OnRuntimeUpdate;
    }
}
