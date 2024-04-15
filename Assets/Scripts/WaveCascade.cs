using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public class WaveCascade
{
    [System.Serializable]
    public struct InitSpectrumData
    {
        public int InitKernel;
        public int WrapperKernel;
        public RenderTexture InitSpectrum;
        public RenderTexture ConjugatedSpectrum;
        public RenderTexture WaveData;
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
        public RenderTexture NormalMap;
        public RenderTexture DisplacementMap;
        public RenderTexture FoamMap;
        public RenderTexture BlurFoamMap;
    }
    
    private OceanSettings _oceanSettings;
    private CascadeSettings _cascadeSettings;
    private Texture2D _gaussianNoise;
    private OceanComputeShaders _computeShaders;
    private CommandBuffer _commandBuffer;
    private ComputeFFT _fft;
    private ComputeBlur _blur;
    
    private InitSpectrumData _initSpectrumData;
    private TimeDependantSpectrumData _timeDependantSpectrumData;
    private SpectrumWrapperData _spectrumWrapperData;
    
    public WaveCascade(OceanSettings oceanSettings, CascadeSettings cascadeSettings, CommandBuffer commandBuffer, ComputeFFT fft, ComputeBlur blur, OceanComputeShaders computeShaders, Texture2D gaussianNoise)
    {
        _oceanSettings = oceanSettings;
        _cascadeSettings = cascadeSettings;
        _gaussianNoise = gaussianNoise;
        _computeShaders = computeShaders;
        _commandBuffer = commandBuffer;
        _fft = fft;
        _blur = blur;
        
        InitRenderTextures();
        GenerateInitSpectrum();
    }
    
    private void InitRenderTextures()
    {
        int size = _oceanSettings.Size;
        
        _initSpectrumData.InitKernel = _computeShaders.InitSpectrum.FindKernel("CalculateInitSpectrum");
        _initSpectrumData.WrapperKernel = _computeShaders.InitSpectrum.FindKernel("ConjugateInitSpectrum");
        _timeDependantSpectrumData.Kernel = _computeShaders.TimeDependantSpectrum.FindKernel("CalculateFrequencyDomain");
        _spectrumWrapperData.Kernel = _computeShaders.SpectrumWrapper.FindKernel("ComputeWrapper");
        
        _initSpectrumData.InitSpectrum = Utilities.CreateRenderTexture(size);
        _initSpectrumData.ConjugatedSpectrum = Utilities.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat);
        _initSpectrumData.WaveData = Utilities.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat);

        _timeDependantSpectrumData.FrequencyDomain = Utilities.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat);
        _timeDependantSpectrumData.HeightMap = Utilities.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat);
        _timeDependantSpectrumData.HeightDerivative = Utilities.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat);
        _timeDependantSpectrumData.HorizontalDisplacementMap = Utilities.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat);
        _timeDependantSpectrumData.JacobianXxZzMap = Utilities.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat);
        _timeDependantSpectrumData.JacobianXzMap = Utilities.CreateRenderTexture(size);
        
        _spectrumWrapperData.DisplacementMap = Utilities.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat);
        _spectrumWrapperData.NormalMap = Utilities.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat);
        _spectrumWrapperData.FoamMap = Utilities.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat);
        _spectrumWrapperData.BlurFoamMap = Utilities.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat);
    }
    
    private void GenerateInitSpectrum()
    {
        int kernel = _initSpectrumData.InitKernel;
        int size = _oceanSettings.Size;
        ComputeShader shader = _computeShaders.InitSpectrum;
    
        // Set variables
        _commandBuffer.SetComputeIntParam(shader, "Size", size);
        _commandBuffer.SetComputeIntParam(shader, "LengthScale", _cascadeSettings.LengthScale);
        _commandBuffer.SetComputeFloatParam(shader, "LowCutoff", _cascadeSettings.LowCutoff);
        _commandBuffer.SetComputeFloatParam(shader, "HighCutoff", _cascadeSettings.HighCutoff);
        _commandBuffer.SetComputeFloatParam(shader, "WindSpeed", _oceanSettings.WindSpeed);
        _commandBuffer.SetComputeFloatParam(shader, "WindAngle", _oceanSettings.WindAngle);
        _commandBuffer.SetComputeFloatParam(shader, "Fetch", _oceanSettings.DistanceToShore * 1000);
        _commandBuffer.SetComputeFloatParam(shader, "Depth", _oceanSettings.Depth);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "Noise", _gaussianNoise);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "InitSpectrum", _initSpectrumData.InitSpectrum);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "WaveData", _initSpectrumData.WaveData);

        // Dispatch the compute shader
        _commandBuffer.DispatchCompute(_computeShaders.InitSpectrum, kernel, size / 8, size / 8, 1);
        
        kernel = _initSpectrumData.WrapperKernel;
        
        _commandBuffer.SetComputeIntParam(shader, "Size", size);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "InitSpectrum", _initSpectrumData.InitSpectrum);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "ConjugatedSpectrum", _initSpectrumData.ConjugatedSpectrum);
        
        _commandBuffer.DispatchCompute(_computeShaders.InitSpectrum, kernel, size / 8, size / 8, 1);
    }

    public void Update(float time)
    {
        GenerateFrequencyDomain(time);
        GenerateHeightMap();
    }
    
    private void GenerateFrequencyDomain(float time)
    {
        int kernel = _timeDependantSpectrumData.Kernel;
        int size = _oceanSettings.Size;
        ComputeShader shader = _computeShaders.TimeDependantSpectrum;
    
        // Set variables
        _commandBuffer.SetComputeFloatParam(shader, "Time", time);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "InitSpectrum", _initSpectrumData.ConjugatedSpectrum);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "WaveData", _initSpectrumData.WaveData);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "FrequencyDomain", _timeDependantSpectrumData.FrequencyDomain);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "HeightDerivative", _timeDependantSpectrumData.HeightDerivative);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "HorizontalDisplacementMap", _timeDependantSpectrumData.HorizontalDisplacementMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "JacobianXxZzMap", _timeDependantSpectrumData.JacobianXxZzMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "JacobianXzMap", _timeDependantSpectrumData.JacobianXzMap);

        // Dispatch the compute shader
        _commandBuffer.DispatchCompute(shader, kernel, size / 8, size / 8, 1);
    }
    
    private void GenerateHeightMap()
    {
        _fft.DoIFFT(_timeDependantSpectrumData.FrequencyDomain, _timeDependantSpectrumData.HeightMap);
        _fft.DoIFFT(_timeDependantSpectrumData.HeightDerivative);
        _fft.DoIFFT(_timeDependantSpectrumData.HorizontalDisplacementMap);
        _fft.DoIFFT(_timeDependantSpectrumData.JacobianXxZzMap);
        _fft.DoIFFT(_timeDependantSpectrumData.JacobianXzMap);

        int kernel = _spectrumWrapperData.Kernel;
        int size = _oceanSettings.Size;
        ComputeShader shader = _computeShaders.SpectrumWrapper;
        
        _commandBuffer.SetComputeFloatParam(shader, "DeltaTime", Time.deltaTime);
        _commandBuffer.SetComputeFloatParam(shader, "DisplacementFactor", _oceanSettings.WaveChopyFactor);
        _commandBuffer.SetComputeFloatParam(shader, "FoamIntensity", _oceanSettings.FoamIntensity);
        _commandBuffer.SetComputeFloatParam(shader, "FoamDecay", _oceanSettings.FoamDecay);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "HeightDerivative", _timeDependantSpectrumData.HeightDerivative);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "HeightMap", _timeDependantSpectrumData.HeightMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "HorizontalDisplacementMap", _timeDependantSpectrumData.HorizontalDisplacementMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "JacobianXxZzMap", _timeDependantSpectrumData.JacobianXxZzMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "JacobianXzMap", _timeDependantSpectrumData.JacobianXzMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "NormalMap", _spectrumWrapperData.NormalMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "DisplacementMap", _spectrumWrapperData.DisplacementMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "FoamMap", _spectrumWrapperData.FoamMap);

        _commandBuffer.DispatchCompute(shader, kernel, size / 8, size / 8, 1);
        
        _blur.Blur(_spectrumWrapperData.FoamMap, _spectrumWrapperData.BlurFoamMap);
    }

    public SpectrumWrapperData GetSpectrumWrapperData()
    {
        return _spectrumWrapperData;
    }
}
