using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipMovement : MonoBehaviour
{
    public float speed = 10f;
    public float rotationSpeed = 100f;

    private Rigidbody _rigidbody;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        float moveVertical = Input.GetAxis("Vertical");
        float moveHorizontal = Input.GetAxis("Horizontal");

        if (moveVertical != 0)
        {
            // Move the ship forward or backward
            _rigidbody.AddForce(transform.forward * speed * moveVertical);
            
            if (moveHorizontal != 0)
            {
                // Rotate the ship only if it's moving forward
                _rigidbody.AddTorque(0f, rotationSpeed * moveHorizontal, 0f);
            }
        }
    }
}