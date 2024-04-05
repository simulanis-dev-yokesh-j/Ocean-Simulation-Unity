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
                Mesh ringMesh = GeneratePlane(_ringSize, ringResolution, planeStartPoint);
                
                GameObject ring = new GameObject("Ring " + ringNumber);
                ring.transform.parent = _parent;
                ring.AddComponent<MeshFilter>().mesh = ringMesh;
                ring.AddComponent<MeshRenderer>().material = material;
            }
        }
    }

    private Mesh GeneratePlane(int size, int resolution, Vector2 startPoint)
    {
        Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1)];
        int[] triangles = new int[resolution * resolution * 6];

        float increment = 1f / resolution;
        int vertIndex = 0;
        int triIndex = 0;

        // Create vertices and triangles
        for (int y = 0; y <= resolution; y++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                vertices[vertIndex] = new Vector3(size * x * increment + startPoint.x, 0, size * y * increment - startPoint.y);

                if (x != resolution && y != resolution)
                {
                    triangles[triIndex] = vertIndex;
                    triangles[triIndex + 1] = vertIndex + resolution + 1;
                    triangles[triIndex + 2] = vertIndex + resolution + 2;

                    triangles[triIndex + 3] = vertIndex;
                    triangles[triIndex + 4] = vertIndex + resolution + 2;
                    triangles[triIndex + 5] = vertIndex + 1;

                    triIndex += 6;
                }
                vertIndex++;
            }
        }

        //Create mesh
        Mesh mesh = new Mesh();
        mesh.name = "Ocean Mesh";
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        return mesh;
    }
}