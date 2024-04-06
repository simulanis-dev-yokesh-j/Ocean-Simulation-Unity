using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class ShipController : MonoBehaviour
{
    public float MoveSpeed = 2f; // Adjust this to control ship movement speed
    public float RotationSpeed = 30; // Adjust this to control rotation speed
    public GameObject Ocean; // Reference to the OceanGenerator component
    
    private Rigidbody rb;

    private void Awake()
    {
        // Get the Rigidbody component
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // Get input for movement
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        // Apply force to the Rigidbody for movement
        if(verticalInput > 0f)
            rb.AddForce(transform.forward * MoveSpeed);

        // Only allow rotation if moving forward
        if (verticalInput > 0f)
        {
            // Calculate rotation based on horizontal input
            Quaternion rotation = Quaternion.Euler(0f, horizontalInput * RotationSpeed * Time.deltaTime, 0f);
            rb.MoveRotation(rb.rotation * rotation);
        }

        // var pos = transform.position;
        // Ocean.transform.position = new Vector3(pos.x, Ocean.transform.position.y, pos.z);
    }
}