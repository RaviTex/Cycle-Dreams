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
    [SerializeField] private bool isCameraAssistEnabled = true;

    [Header("State Settings")]
    public bool isDriving = false;
    [SerializeField] private float returnToCenterSpeed = 2f;
    [SerializeField] private float snapToBoundsSpeed = 10f;
    [SerializeField] private Vector2 drivingRotationClampX = new Vector2(-45f, 45f);
    [SerializeField] private Vector2 drivingRotationClampY = new Vector2(-60f, 60f);

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

        bool isOutOfBounds = false;
        if (isDriving)
        {
            isOutOfBounds = rotationX < drivingRotationClampX.x || rotationX > drivingRotationClampX.y ||
                            rotationY < drivingRotationClampY.x || rotationY > drivingRotationClampY.y;
        }

        if (!isDriving || !isOutOfBounds)
        {
            rotationX -= lookInput.y;
            rotationY += lookInput.x;
        }

        if (isDriving)
        {
            if (isOutOfBounds)
            {
                float targetX = Mathf.Clamp(rotationX, drivingRotationClampX.x, drivingRotationClampX.y);
                float targetY = Mathf.Clamp(rotationY, drivingRotationClampY.x, drivingRotationClampY.y);

                rotationX = Mathf.Lerp(rotationX, targetX, Time.deltaTime * snapToBoundsSpeed);
                rotationY = Mathf.Lerp(rotationY, targetY, Time.deltaTime * snapToBoundsSpeed);

                if (Mathf.Abs(rotationX - targetX) < 0.1f && Mathf.Abs(rotationY - targetY) < 0.1f)
                {
                    rotationX = targetX;
                    rotationY = targetY;
                }
            }
            else
            {
                rotationX = Mathf.Clamp(rotationX, drivingRotationClampX.x, drivingRotationClampX.y);
                rotationY = Mathf.Clamp(rotationY, drivingRotationClampY.x, drivingRotationClampY.y);

                if (lookInput.sqrMagnitude < 0.001f && isCameraAssistEnabled)
                {
                    rotationX = Mathf.Lerp(rotationX, 0f, Time.deltaTime * returnToCenterSpeed);
                    rotationY = Mathf.Lerp(rotationY, 0f, Time.deltaTime * returnToCenterSpeed);
                }
            }
        }
        else
        {
            rotationX = Mathf.Clamp(rotationX, -90f, 90f);
        }

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
