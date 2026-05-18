using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public Transform pivot;
    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private float gamepadSensitivity = 1f;
    public InputActionReference lookAction;
    public bool isVRMode = false;
    [SerializeField] private bool lerpPositionAndRotation = true;
    [SerializeField] private float lerpSpeed = 5f;

    private float rotationX = 0f;
    private float rotationY = 0f;
    private GameObject camera;
    private Vector2 lookInput;


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

        rotationX -= lookInput.y;
        rotationY += lookInput.x;

        rotationX = Mathf.Clamp(rotationX, -45f, 45f);
        rotationY = Mathf.Clamp(rotationY, -120f, 120f);

        Quaternion deviation = Quaternion.Euler(rotationX, rotationY, 0);
        camera.transform.localRotation = deviation;
    }
    void OnEnable()
    {
        if (lookAction != null)
            lookAction.action.Enable();
        lookAction.action.performed += ctx =>
        {
            lookInput = ctx.ReadValue<Vector2>();
            if (ctx.control.device is Gamepad)
            {
                lookInput *= gamepadSensitivity;
            }
            if (ctx.control.device is not Gamepad)
            {
                lookInput *= sensitivity;
            }
        };
        lookAction.action.canceled += ctx => lookInput = Vector2.zero;
    }

    void OnDisable()
    {
        if (lookAction != null)
            lookAction.action.Disable();
    }
}
