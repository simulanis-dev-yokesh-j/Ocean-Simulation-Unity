using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class ComputeCustomFFT
{
    //private RawImage _buterflyTextureImage;
    
    private CommandBuffer _commandBuffer;
    private ComputeShader _fftComputeShader;
    private RenderTexture _butterflyTexture;
    private RenderTexture _pingPong1;
    
    private int _buterflyComputeKernel;
    private int _horizontalOperationKernel;
    private int _verticalOperationKernel;
    private int _permuteAndScaleKernel;
    private int _size;
    private int _logSize;

    public ComputeCustomFFT(int size, CommandBuffer commandBuffer, ComputeShader fftShader)
    {
        _size = size;
        _fftComputeShader = fftShader;
        _commandBuffer = commandBuffer;
        _logSize = (int)Mathf.Log(_size, 2);
        
        _buterflyComputeKernel = _fftComputeShader.FindKernel("ComputeButterflyTexture");
        _horizontalOperationKernel = _fftComputeShader.FindKernel("HorizontalOperation");
        _verticalOperationKernel = _fftComputeShader.FindKernel("VerticalOperation");
        _permuteAndScaleKernel = _fftComputeShader.FindKernel("PermuteAndScale");

        _butterflyTexture = CreateRenderTexture(_logSize, _size, RenderTextureFormat.ARGBHalf);
        _pingPong1 = CreateRenderTexture(_size, _size);
        
        ComputeButerflyTexture();
        
    }

    private void ComputeButerflyTexture()
    {
        _commandBuffer.SetComputeIntParam(_fftComputeShader, "Size", _size);
        _commandBuffer.SetComputeIntParam(_fftComputeShader, "LogSize", _logSize);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _buterflyComputeKernel, "ButterflyTexture", _butterflyTexture);

        _commandBuffer.DispatchCompute(_fftComputeShader, _buterflyComputeKernel, _logSize, _size/8, 1);
    }

    public static RenderTexture CreateRenderTexture(int width, int height, RenderTextureFormat format = RenderTextureFormat.RGFloat, bool useMips = false)
    {
        RenderTexture rt = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear);
        rt.useMipMap = useMips;
        rt.autoGenerateMips = false;
        rt.anisoLevel = 6;
        rt.filterMode = FilterMode.Trilinear;
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    public void DoIFFT(RenderTexture input, RenderTexture output)
    {
        int pingPong = 0;
        
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _horizontalOperationKernel, "ButterflyTexture", _butterflyTexture);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _horizontalOperationKernel, "PingPong0", input);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _horizontalOperationKernel, "PingPong1", _pingPong1);
        
        for(int stage = 0; stage < _logSize; stage++)
        {
            _commandBuffer.SetComputeIntParam(_fftComputeShader, "PingPong", pingPong);
            _commandBuffer.SetComputeIntParam(_fftComputeShader, "Stage", stage);
            _commandBuffer.DispatchCompute(_fftComputeShader, _horizontalOperationKernel, _size / 8, _size / 8, 1);
            pingPong = (pingPong + 1) % 2;
        }
        
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _verticalOperationKernel, "ButterflyTexture", _butterflyTexture);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _verticalOperationKernel, "PingPong0", input);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _verticalOperationKernel, "PingPong1", _pingPong1);
        
        for(int stage = 0; stage < _logSize; stage++)
        {
            _commandBuffer.SetComputeIntParam(_fftComputeShader, "PingPong", pingPong);
            _commandBuffer.SetComputeIntParam(_fftComputeShader, "Stage", stage);
            _commandBuffer.DispatchCompute(_fftComputeShader, _verticalOperationKernel, _size / 8, _size / 8, 1);
            pingPong = (pingPong + 1) % 2;
        }

        _commandBuffer.SetComputeIntParam(_fftComputeShader, "Size", _size);
        _commandBuffer.SetComputeIntParam(_fftComputeShader, "PingPong", pingPong);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _permuteAndScaleKernel, "PingPong0", input);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _permuteAndScaleKernel, "PingPong1", _pingPong1);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _permuteAndScaleKernel, "TimeDomain", output);
        _commandBuffer.DispatchCompute(_fftComputeShader, _permuteAndScaleKernel, _size / 8, _size / 8, 1);
    }

    public RenderTexture GetButterflyTexture()
    {
        return _butterflyTexture;
    }
    
    public RenderTexture GetPingPong1()
    {
        return _pingPong1;
    }
}
