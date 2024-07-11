using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class ComputeFFT
{
    //private RawImage _buterflyTextureImage;
    
    private CommandBuffer _commandBuffer;
    private ComputeShader _fftComputeShader;
    private RenderTexture _butterflyTexture;
    private RenderTexture _pingPong1;
    
    private int _buterflyComputeKernel;
    private int _horizontalOperationKernel;
    private int _verticalOperationKernel;
    private int _copyToPingPong1Kernel;
    private int _permuteAndScaleKernel;
    private int _size;
    private int _logSize;

    public ComputeFFT(int size, CommandBuffer commandBuffer, ComputeShader fftShader)
    {
        _size = size;
        _fftComputeShader = fftShader;
        _commandBuffer = commandBuffer;
        _logSize = (int)Mathf.Log(_size, 2);
        
        _buterflyComputeKernel = _fftComputeShader.FindKernel("ComputeButterflyTexture");
        _horizontalOperationKernel = _fftComputeShader.FindKernel("HorizontalOperation");
        _verticalOperationKernel = _fftComputeShader.FindKernel("VerticalOperation");
        _copyToPingPong1Kernel = _fftComputeShader.FindKernel("CopyToPingPong1");
        _permuteAndScaleKernel = _fftComputeShader.FindKernel("PermuteAndScale");

        _butterflyTexture = Utilities.CreateRenderTexture(_logSize, _size, RenderTextureFormat.ARGBFloat);
        _pingPong1 = Utilities.CreateRenderTexture(_size, _size, RenderTextureFormat.ARGBFloat);
        
        ComputeButerflyTexture();
        
    }

    private void ComputeButerflyTexture()
    {
        _commandBuffer.SetComputeIntParam(_fftComputeShader, "Size", _size);
        _commandBuffer.SetComputeIntParam(_fftComputeShader, "LogSize", _logSize);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _buterflyComputeKernel, "ButterflyTexture", _butterflyTexture);

        _commandBuffer.DispatchCompute(_fftComputeShader, _buterflyComputeKernel, _logSize, _size/8, 1);
    }

    public void DoIFFT(RenderTexture input, RenderTexture output = null)
    {
        int pingPong = 0;
        RenderTexture pingPong0 = input;

        if (output != null)
        {
            // Copy to pingPong1
            _commandBuffer.SetComputeTextureParam(_fftComputeShader, _copyToPingPong1Kernel, "PingPong0", input);
            _commandBuffer.SetComputeTextureParam(_fftComputeShader, _copyToPingPong1Kernel, "PingPong1", output);
            _commandBuffer.DispatchCompute(_fftComputeShader, _copyToPingPong1Kernel, _size / 8, _size / 8, 1);
            pingPong0 = output;
        }

        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _horizontalOperationKernel, "ButterflyTexture", _butterflyTexture);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _horizontalOperationKernel, "PingPong0", pingPong0);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _horizontalOperationKernel, "PingPong1", _pingPong1);
        
        for(int stage = 0; stage < _logSize; stage++)
        {
            _commandBuffer.SetComputeIntParam(_fftComputeShader, "PingPong", pingPong);
            _commandBuffer.SetComputeIntParam(_fftComputeShader, "Stage", stage);
            _commandBuffer.DispatchCompute(_fftComputeShader, _horizontalOperationKernel, _size / 8, _size / 8, 1);
            pingPong = (pingPong + 1) % 2;
        }
        
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _verticalOperationKernel, "ButterflyTexture", _butterflyTexture);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _verticalOperationKernel, "PingPong0", pingPong0);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _verticalOperationKernel, "PingPong1", _pingPong1);
        
        for(int stage = 0; stage < _logSize; stage++)
        {
            _commandBuffer.SetComputeIntParam(_fftComputeShader, "PingPong", pingPong);
            _commandBuffer.SetComputeIntParam(_fftComputeShader, "Stage", stage);
            _commandBuffer.DispatchCompute(_fftComputeShader, _verticalOperationKernel, _size / 8, _size / 8, 1);
            pingPong = (pingPong + 1) % 2;
        }

        // Copy to pingPong1
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _copyToPingPong1Kernel, "PingPong0", pingPong0);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _copyToPingPong1Kernel, "PingPong1", _pingPong1);
        _commandBuffer.DispatchCompute(_fftComputeShader, _copyToPingPong1Kernel, _size / 8, _size / 8, 1);
        
        // Permute and scale
        _commandBuffer.SetComputeIntParam(_fftComputeShader, "Size", _size);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _permuteAndScaleKernel, "PingPong0", pingPong0);
        _commandBuffer.SetComputeTextureParam(_fftComputeShader, _permuteAndScaleKernel, "PingPong1", _pingPong1);
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
