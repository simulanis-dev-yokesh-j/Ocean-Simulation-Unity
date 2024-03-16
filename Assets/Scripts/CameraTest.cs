using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraTest : MonoBehaviour
{
    private void Start()
    {
        Camera.main.depthTextureMode = DepthTextureMode.Depth;
    }
}
