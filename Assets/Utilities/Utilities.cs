using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class Utilities
{
    public static RenderTexture CreateRenderTexture(int size, RenderTextureFormat format = RenderTextureFormat.RGFloat, bool useMips = false)
    {
        return CreateRenderTexture(size, size, format, useMips);
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
    
    public static void PrintTexture(RenderTexture renderTexture)
    {
        // Create a new Texture2D with a format that supports negative values
        Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAHalf, false);

        // Set the active RenderTexture
        RenderTexture.active = renderTexture;

        // Read the pixels from the RenderTexture
        texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);

        // Apply the changes
        texture2D.Apply();

        // Reset the active RenderTexture
        RenderTexture.active = null;

        // Now you have a Texture2D object with the data from the RenderTexture
        // You can access the pixel data, keeping in mind that the values can be negative

        Vector3 lowestX = new Vector3(10000000, 10000000, 10000000);
        Vector3 highestX = new Vector3(-10000000, -10000000, -10000000);
        Vector3 lowestY = new Vector3(10000000, 10000000, 10000000);
        Vector3 highestY = new Vector3(-10000000, -10000000, -10000000);

        // Print the pixel values
        for (int i = 0; i < texture2D.width; i++)
        {
            for (int j = 0; j < texture2D.height; j++)
            {
                Color pixelColor = texture2D.GetPixel(i, j);
                Debug.Log("Pixel (" + i + ", " + j + "): " + pixelColor);

                if (pixelColor.r < lowestX.x)
                {
                    lowestX.x = pixelColor.r;
                    lowestX.y = i;
                    lowestX.z = j;
                } 
                
                if (pixelColor.r > highestX.x)
                {
                    highestX.x = pixelColor.r;
                    highestX.y = i;
                    highestX.z = j;
                }
                
                if (pixelColor.g < lowestY.x)
                {
                    lowestY.x = pixelColor.g;
                    lowestY.y = i;
                    lowestY.z = j;
                }
                
                if (pixelColor.g > highestY.x)
                {
                    highestY.x = pixelColor.g;
                    highestY.y = i;
                    highestY.z = j;
                }
            }
        }
        
        Debug.Log($"Lowest X: {lowestX.x} at ({lowestX.y}, {lowestX.z})");
        Debug.Log($"Highest X: {highestX.x} at ({highestX.y}, {highestX.z})");
        Debug.Log($"Lowest Y: {lowestY.x} at ({lowestY.y}, {lowestY.z})");
        Debug.Log($"Highest Y: {highestY.x} at ({highestY.y}, {highestY.z})");
    }
    
    public static void OutputTexture(RenderTexture renderTexture)
    {
        // Create a new Texture2D with a format that supports negative values
        Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAHalf, false);

        // Set the active RenderTexture
        RenderTexture.active = renderTexture;

        // Read the pixels from the RenderTexture
        texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);

        // Apply the changes
        texture2D.Apply();

        // Reset the active RenderTexture
        RenderTexture.active = null;

        // Prepare the file for writing
        using (StreamWriter writer = new StreamWriter("./Assets/my_output.txt"))
        {
            // Print the pixel values
            for (int i = 0; i < texture2D.width; i++)
            {
                for (int j = 0; j < texture2D.height; j++)
                {
                    Color pixelColor = texture2D.GetPixel(i, j);
                    writer.WriteLine("Pixel (" + i + ", " + j + "): " + pixelColor);
                }
            }
        }
    }
}
