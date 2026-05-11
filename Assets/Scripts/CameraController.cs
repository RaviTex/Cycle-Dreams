using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public Transform pivot;
    public float sensitivity = 0.1f;
    public InputActionReference lookAction;
    public bool isVRMode = false;
    [SerializeField] private bool lerpPositionAndRotation = true;
    [SerializeField] private float lerpSpeed = 5f;

    private float rotationX = 0f;
    private float rotationY = 0f;
    private GameObject camera;


    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        camera = GetComponentInChildren<Camera>().gameObject;
    }

    void LateUpdate()
    {
        if (pivot == null) return;

        if (lerpPositionAndRotation)
        {
            transform.position = Vector3.Lerp(transform.position, pivot.position, Time.deltaTime * lerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, pivot.rotation, Time.deltaTime * lerpSpeed);
        }
        else
        {
            transform.position = pivot.position;
            transform.rotation = pivot.rotation;
        }

        if (isVRMode)
        {
            return;
        }
        Vector2 lookInput = lookAction != null ? lookAction.action.ReadValue<Vector2>() : Vector2.zero;

        rotationX -= lookInput.y * sensitivity;
        rotationY += lookInput.x * sensitivity;

        rotationX = Mathf.Clamp(rotationX, -45f, 45f);
        rotationY = Mathf.Clamp(rotationY, -120f, 120f);

        Quaternion deviation = Quaternion.Euler(rotationX, rotationY, 0);
        camera.transform.localRotation = deviation;
    }
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
}
