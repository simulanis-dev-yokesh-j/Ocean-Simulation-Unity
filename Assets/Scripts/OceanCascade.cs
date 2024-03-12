using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public class OceanCascade
{
    [System.Serializable]
    public struct InitSpectrumData
    {
        public int InitKernel;
        public int WrapperKernel;
        public RenderTexture InitSpectrum;
        public RenderTexture ConjugatedSpectrum;
        public RenderTexture WaveData;
        public Texture2D GaussianNoise;
    }

    [System.Serializable]
    public struct TimeDependantSpectrumData
    {
        public int Kernel;
        public RenderTexture FrequencyDomain;
        public RenderTexture HeightMap;
        public RenderTexture HeightDerivative;
        public RenderTexture HorizontalDisplacementMap;
        public RenderTexture JacobianXxZzMap;
        public RenderTexture JacobianXzMap;
    }
    
    [System.Serializable]
    public struct SpectrumWrapperData
    {
        public int Kernel;
        public RenderTexture FoamMap;
    }

    // Outputs
    private InitSpectrumData _initSpectrumData;
    private TimeDependantSpectrumData _timeDependantSpectrumData;
    private SpectrumWrapperData _spectrumWrapperData;

    // other
    private int _size;
    private int _lengthScale;
    private float _lowerCutOff;
    private float _upperCutOff;
    private CompShaders _computeShaders;
    private CommandBuffer _commandBuffer;
    private ComputeFFT _fft;
    private OceanSettings _oceanSettings;

    public OceanCascade(CompShaders computeShaders, CommandBuffer commandBuffer)
    {
        _computeShaders = computeShaders;
        _commandBuffer = commandBuffer;
    }

    public void Init(int size, int lengthScale, float lowerCutOff, float upperCutOff, OceanSettings oceanSettings, ComputeFFT fft, Texture2D gaussianNoise)
    {
        _size = size;
        _fft = fft;
        _lengthScale = lengthScale;
        _lowerCutOff = lowerCutOff;
        _upperCutOff = upperCutOff;
        _oceanSettings = oceanSettings;
        
        InitRenderTextures(gaussianNoise);
        GenerateInitSpectrum();
    }
    
    private void InitRenderTextures(Texture2D gaussianNoise)
    {
        _initSpectrumData.InitKernel = _computeShaders.InitSpectrum.FindKernel("CalculateInitSpectrum");
        _initSpectrumData.WrapperKernel = _computeShaders.InitSpectrum.FindKernel("ConjugateInitSpectrum");
        _timeDependantSpectrumData.Kernel = _computeShaders.TimeDependantSpectrum.FindKernel("CalculateFrequencyDomain");
        _spectrumWrapperData.Kernel = _computeShaders.SpectrumWrapper.FindKernel("ComputeWrapper");

        _initSpectrumData.InitSpectrum = Utilities.CreateRenderTexture(_size);
        _initSpectrumData.ConjugatedSpectrum = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _initSpectrumData.WaveData = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _initSpectrumData.GaussianNoise = gaussianNoise;

        _timeDependantSpectrumData.FrequencyDomain = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _timeDependantSpectrumData.HeightMap = Utilities.CreateRenderTexture(_size);
        _timeDependantSpectrumData.HeightDerivative = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _timeDependantSpectrumData.HorizontalDisplacementMap = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _timeDependantSpectrumData.JacobianXxZzMap = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _timeDependantSpectrumData.JacobianXzMap = Utilities.CreateRenderTexture(_size);
        
        _spectrumWrapperData.FoamMap = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
    }
    
    private void GenerateInitSpectrum()
    {
        int kernel = _initSpectrumData.InitKernel;
        ComputeShader shader = _computeShaders.InitSpectrum;
    
        // Set variables
        _commandBuffer.SetComputeIntParam(shader, "Size", _size);
        _commandBuffer.SetComputeIntParam(shader, "LengthScale", _lengthScale);
        _commandBuffer.SetComputeFloatParam(shader, "LowerCutOff", _lowerCutOff);
        _commandBuffer.SetComputeFloatParam(shader, "UpperCutOff", _upperCutOff);
        _commandBuffer.SetComputeFloatParam(shader, "WindSpeed", _oceanSettings.WindSpeed);
        _commandBuffer.SetComputeFloatParam(shader, "WindAngle", _oceanSettings.WindAngle);
        _commandBuffer.SetComputeFloatParam(shader, "Fetch", _oceanSettings.DistanceToShore * 1000);
        _commandBuffer.SetComputeFloatParam(shader, "Depth", _oceanSettings.Depth);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "Noise", _initSpectrumData.GaussianNoise);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "InitSpectrum", _initSpectrumData.InitSpectrum);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "WaveData", _initSpectrumData.WaveData);

        // Dispatch the compute shader
        _commandBuffer.DispatchCompute(_computeShaders.InitSpectrum, kernel, _size / 8, _size / 8, 1);
        
        kernel = _initSpectrumData.WrapperKernel;
        
        _commandBuffer.SetComputeIntParam(shader, "Size", _size);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "InitSpectrum", _initSpectrumData.InitSpectrum);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "ConjugatedSpectrum", _initSpectrumData.ConjugatedSpectrum);
        
        _commandBuffer.DispatchCompute(_computeShaders.InitSpectrum, kernel, _size / 8, _size / 8, 1);
    }

    public void Update(float time)
    {
        GenerateFrequencyDomain(time);
        GenerateHeightMap();
    }
    
    private void GenerateFrequencyDomain(float time)
    {
        int kernel = _timeDependantSpectrumData.Kernel;
        ComputeShader shader = _computeShaders.TimeDependantSpectrum;
    
        // Set variables
        _commandBuffer.SetComputeFloatParam(shader, "Time", time);
        _commandBuffer.SetComputeFloatParam(shader, "DisplacementFactor", _oceanSettings.WaveChopyFactor);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "InitSpectrum", _initSpectrumData.ConjugatedSpectrum);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "WaveData", _initSpectrumData.WaveData);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "FrequencyDomain", _timeDependantSpectrumData.FrequencyDomain);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "HeightDerivative", _timeDependantSpectrumData.HeightDerivative);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "HorizontalDisplacementMap", _timeDependantSpectrumData.HorizontalDisplacementMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "JacobianXxZzMap", _timeDependantSpectrumData.JacobianXxZzMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "JacobianXzMap", _timeDependantSpectrumData.JacobianXzMap);

        // Dispatch the compute shader
        _commandBuffer.DispatchCompute(shader, kernel, _size / 8, _size / 8, 1);
    }
    
    private void GenerateHeightMap()
    {
        // Do IFFT
        _fft.DoIFFT(_timeDependantSpectrumData.FrequencyDomain, _timeDependantSpectrumData.HeightMap);
        _fft.DoIFFT(_timeDependantSpectrumData.HeightDerivative);
        _fft.DoIFFT(_timeDependantSpectrumData.HorizontalDisplacementMap);
        _fft.DoIFFT(_timeDependantSpectrumData.JacobianXxZzMap);
        _fft.DoIFFT(_timeDependantSpectrumData.JacobianXzMap);

        // Compute foam
        int kernel = _spectrumWrapperData.Kernel;
        ComputeShader shader = _computeShaders.SpectrumWrapper;
        
        _commandBuffer.SetComputeFloatParam(shader, "DeltaTime", Time.deltaTime);
        _commandBuffer.SetComputeFloatParam(shader, "DisplacementFactor", _oceanSettings.WaveChopyFactor);
        _commandBuffer.SetComputeFloatParam(shader, "FoamIntensity", _oceanSettings.FoamIntensity);
        _commandBuffer.SetComputeFloatParam(shader, "FoamDecay", _oceanSettings.FoamDecay);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "JacobianXxZzMap", _timeDependantSpectrumData.JacobianXxZzMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "JacobianXzMap", _timeDependantSpectrumData.JacobianXzMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "FoamMap", _spectrumWrapperData.FoamMap);

        _commandBuffer.DispatchCompute(shader, kernel, _size / 8, _size / 8, 1);
    }
    
    public InitSpectrumData GetInitSpectrumData()
    {
        return _initSpectrumData;
    }
    
    public TimeDependantSpectrumData GetTimeDependantSpectrumData()
    {
        return _timeDependantSpectrumData;
    }
    
    public SpectrumWrapperData GetSpectrumWrapperData()
    {
        return _spectrumWrapperData;
    }
}
