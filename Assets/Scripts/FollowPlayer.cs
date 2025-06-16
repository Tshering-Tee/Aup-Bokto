using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public Transform target;
    public float trailDistance = 2.0f;  // Distance behind the target
    public float heightOffset = 1.5f;   // Vertical offset from the target
    public float smoothTime = 0.3f;     // Time for the camera to reach the target

    private Vector3 currentVelocity = Vector3.zero;

    // LateUpdate is called once per frame, after all Update functions have been called
    void LateUpdate()
    {
        // Calculate the desired camera position
        Vector3 desiredPosition = target.position - target.forward * trailDistance;
        desiredPosition.y += heightOffset;

        // Smoothly damp the camera's position towards the desired position
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothTime);

        // Make the camera look at the target
        transform.LookAt(target.transform);
    }
}