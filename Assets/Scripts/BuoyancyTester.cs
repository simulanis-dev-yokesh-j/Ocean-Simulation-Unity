using UnityEngine;

public class BuoyancyTester : MonoBehaviour
{
    [SerializeField] private OceanGenerator oceanGenerator;
    
    private void Start()
    {
        if (oceanGenerator == null)
            oceanGenerator = FindObjectOfType<OceanGenerator>();
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            TestOceanSampling();
        }
    }
    
    private void TestOceanSampling()
    {
        Vector3 testPosition = transform.position;
        
        Debug.Log("=== OCEAN SAMPLING TEST ===");
        Debug.Log($"Test Position: {testPosition}");
        
        if (oceanGenerator == null)
        {
            Debug.LogError("No Ocean Generator found!");
            return;
        }
        
        // Test each cascade
        for (int i = 0; i < 3; i++)
        {
            var displacementMap = oceanGenerator.GetDisplacementMap(i);
            var normalMap = oceanGenerator.GetNormalMap(i);
            float lengthScale = oceanGenerator.GetLengthScale(i);
            
            Debug.Log($"Cascade {i}: LengthScale={lengthScale}, " +
                     $"DisplacementMap={(displacementMap != null ? "OK" : "NULL")}, " +
                     $"NormalMap={(normalMap != null ? "OK" : "NULL")}");
        }
        
        // Test ocean data reader
        Vector3 displacement = OceanDataReader.SampleOceanHeight(testPosition, oceanGenerator);
        Vector3 normal = OceanDataReader.SampleOceanNormal(testPosition, oceanGenerator);
        
        Debug.Log($"Ocean Height at position: {displacement.y}");
        Debug.Log($"Ocean Normal: {normal}");
        Debug.Log($"Total Displacement: {displacement}");
        
        if (Mathf.Abs(displacement.y) < 0.001f)
        {
            Debug.LogWarning("Ocean displacement is very small! Check your ocean wave settings.");
        }
    }
    
    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 300, 20), "Press 'T' to test ocean sampling");
        
        if (oceanGenerator != null)
        {
            Vector3 displacement = OceanDataReader.SampleOceanHeight(transform.position, oceanGenerator);
            GUI.Label(new Rect(10, 30, 300, 20), $"Ocean Height: {displacement.y:F3}");
            GUI.Label(new Rect(10, 50, 300, 20), $"Position Y: {transform.position.y:F3}");
        }
    }
} 