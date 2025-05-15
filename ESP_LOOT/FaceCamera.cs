using UnityEngine;

public class FaceCamera : MonoBehaviour
{
     private Transform cameraTransform;
    
    void Start()
    {
        // Cache main camera reference for better performance
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }
    
    void Update()
    {
        // Use cached camera transform if available, otherwise try to get it again
        if (cameraTransform == null)
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            return;
        }
        transform.rotation = Quaternion.LookRotation(transform.position - cameraTransform.position);
    }
}
