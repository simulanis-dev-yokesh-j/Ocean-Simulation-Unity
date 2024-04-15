using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ComputeBlur
{
    private CommandBuffer _commandBuffer;
    private ComputeShader _blurComputeShader;
    private RenderTexture _data;

    private int _size;
    private int _kernelSize = 2;
    private float[] _kernel = new float[5];
    private ComputeBuffer _kernelBuffer;
    private int _copyToOutputKernel;
    private int _horizontalBlurKernel;
    private int _verticalBlurKernel;
    
    public ComputeBlur(int size, CommandBuffer commandBuffer, ComputeShader blurShader)
    {
        _size = size;
        _blurComputeShader = blurShader;
        _commandBuffer = commandBuffer;
        
        _copyToOutputKernel = _blurComputeShader.FindKernel("CopyToOutput");
        _horizontalBlurKernel = _blurComputeShader.FindKernel("HorizontalBlur");
        _verticalBlurKernel = _blurComputeShader.FindKernel("VerticalBlur");

        InitKernel();
    }

    private void InitKernel()
    {
        _kernel[0] = 0.0223f;
        _kernel[1] = 0.2299f;
        _kernel[2] = 0.4954f;
        _kernel[3] = 0.2299f;
        _kernel[4] = 0.0223f;

        _kernelBuffer = new ComputeBuffer(_kernel.Length, sizeof(float));
        _kernelBuffer.SetData(_kernel);
    }

    public void Blur(RenderTexture input, RenderTexture output = null)
    {
        if (output != null)
        {
            _commandBuffer.SetComputeTextureParam(_blurComputeShader, _copyToOutputKernel, "Input", input);
            _commandBuffer.SetComputeTextureParam(_blurComputeShader, _copyToOutputKernel, "Data", output);
            _commandBuffer.DispatchCompute(_blurComputeShader, _copyToOutputKernel, _size / 8, _size / 8, 1);
            _data = output;
        }

        _commandBuffer.SetComputeIntParam(_blurComputeShader, "Size", _size);
        _commandBuffer.SetComputeIntParam(_blurComputeShader, "KernelSize", _kernelSize);
        
        _commandBuffer.SetComputeBufferParam(_blurComputeShader, _horizontalBlurKernel, "Kernel", _kernelBuffer);
        _commandBuffer.SetComputeTextureParam(_blurComputeShader, _horizontalBlurKernel, "Data", _data);
        _commandBuffer.DispatchCompute(_blurComputeShader, _horizontalBlurKernel, _size / 8, _size / 8, 1);

        _commandBuffer.SetComputeBufferParam(_blurComputeShader, _verticalBlurKernel, "Kernel", _kernelBuffer);
        _commandBuffer.SetComputeTextureParam(_blurComputeShader, _verticalBlurKernel, "Data", _data);
        _commandBuffer.DispatchCompute(_blurComputeShader, _verticalBlurKernel, _size / 8, _size / 8, 1);
    }
}
