using UnityEngine;
using UnityEngine.Rendering;

public class OceanGeometry : MonoBehaviour
{
    [SerializeField] private Transform _parent;
    [SerializeField] private int _ringSize = 512;
    [SerializeField] private int _resolution = 512;
    [SerializeField] private int _numRings = 5;

    public void GenerateGrid(Material material)
    {
        for (int i = 0; i < _numRings; i++)
        {
            GenerateRing(i, material);
        }
    }

    private void GenerateRing(int ringNumber, Material material)
    {
        int ringResolution = Mathf.Max(10, (int)(_resolution / Mathf.Pow(2, ringNumber)));
        
        float offset = ringNumber * _ringSize + _ringSize * 0.5f;
        Vector2 startPoint = new Vector2(-offset, offset);
        int meshCount = ringNumber * 2 + 1;
        
        for (int z = 0; z < meshCount; z++)
        {
            for (int x = 0; x < meshCount; x++)
            {
                if(z != 0 && x != 0 && z != meshCount - 1 && x != meshCount - 1)
                    continue;

                Vector2 planeStartPoint = new Vector2(startPoint.x + x * _ringSize, startPoint.y - z * _ringSize);
                Mesh ringMesh = GenerateHorizontal(250, 250, 2, true);
                
                GameObject ring = new GameObject("Ring " + ringNumber);
                ring.transform.parent = _parent;

                ring.transform.position = new Vector3(ring.transform.position.x,
                                                        ring.transform.position.y - ringNumber * 5, 
                                                        ring.transform.position.z);

                ring.AddComponent<MeshFilter>().mesh = ringMesh;
                MeshRenderer meshRenderer = ring.AddComponent<MeshRenderer>();
                meshRenderer.material = material;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }

    public Mesh GenerateHorizontal(int width, int height, float unitLength, bool isHorizontal)
    {
        var halfWidth = width * unitLength / 2f;
        var halfHeight = height * unitLength / 2f;
        var positionDelta =
            isHorizontal ? new Vector3(-halfWidth, 0, -halfHeight) : new Vector3(-halfWidth, -halfHeight); 
        width++;
        height++;
        var direction = isHorizontal ? Vector3.forward : Vector3.up;
        var verticesCount = width * height;
        var triangleCount = (width - 1) * (height - 1) * 2;
        var vertices = new Vector3[verticesCount];
        var uvs = new Vector2[verticesCount];
        var triangles = new int[triangleCount * 3];
        var trisIndex = 0;
        for (var w = 0; w < width; w++)
        {
            for (var h = 0; h < height; h++)
            {
                var vertIndex = h * width + w;
                var position = Vector3.right * w * unitLength + direction * h * unitLength;
                vertices[vertIndex] = position + positionDelta;
                uvs[vertIndex] = new Vector2(w / (width - 1f), h / (height - 1f));
                if (w == width - 1 || h == height - 1)
                {
                    continue;
                }

                triangles[trisIndex++] = vertIndex;
                triangles[trisIndex++] = vertIndex + width;
                triangles[trisIndex++] = vertIndex + width + 1;
                triangles[trisIndex++] = vertIndex;
                triangles[trisIndex++] = vertIndex + width + 1;
                triangles[trisIndex++] = vertIndex + 1;
            }
        }

        var mesh = new Mesh {vertices = vertices, triangles = triangles, uv = uvs};
        // mesh.indexFormat = IndexFormat.UInt32;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        
        return mesh;
    }

}