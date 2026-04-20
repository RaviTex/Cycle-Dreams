using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public Transform pivot;
    public float sensitivity = 0.1f;
    public float distance = 5f;

    public InputActionReference lookAction;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void OnEnable()
    {
        if (lookAction != null)
            lookAction.action.Enable();
    }

    void OnDisable()
    {
        if (lookAction != null)
            lookAction.action.Disable();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void LateUpdate()
    {
        if (pivot == null) return;

        Vector2 lookInput = lookAction != null ? lookAction.action.ReadValue<Vector2>() : Vector2.zero;

        rotationX -= lookInput.y * sensitivity;
        rotationY += lookInput.x * sensitivity;

        rotationX = Mathf.Clamp(rotationX, -45f, 45f);
        rotationY = Mathf.Clamp(rotationY, -120f, 120f);

        Quaternion deviation = Quaternion.Euler(rotationX, rotationY, 0);
        Quaternion finalRotation = pivot.rotation * deviation;

        transform.position = pivot.position - (finalRotation * Vector3.forward * distance);
        transform.LookAt(pivot);
    }
}
