using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private float gamepadSensitivity = 1f;
    public InputActionReference lookAction;
    [SerializeField] private bool lerpPositionAndRotation = true;
    [SerializeField] private float lerpSpeed = 5f;
    [SerializeField] private bool isCameraAssistEnabled = true;

    [Header("State Settings")]
    [SerializeField] private float returnToCenterSpeed = 2f;
    [SerializeField] private float snapToBoundsSpeed = 10f;
    [SerializeField] private float snapCompletionThreshold = 0.1f;
    [SerializeField] private Vector2 drivingRotationClampX = new Vector2(-45f, 45f);
    [SerializeField] private Vector2 drivingRotationClampY = new Vector2(-60f, 60f);

    private bool isVRMode
    {
        get
        {
            var gm = GameManager.Instance;
            return gm != null && gm.isInVRMode;
        }
    }
    private bool isModeSwitchPossible
    {
        get
        {
            var gm = GameManager.Instance;
            return gm != null && gm.isModeSwitchPossible;
        }
    }
    private Transform pivot
    {
        get
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.cameraPivot == null) return null;
            return gm.cameraPivot.transform;
        }
    }
    private float rotationX = 0f;
    private float rotationY = 0f;
    private GameObject _camera
    {
        get
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.mainCamera == null) return null;
            return gm.mainCamera.gameObject;
        }
    }
    private Vector2 lookInput;

    void LateUpdate()
    {
        if (pivot == null || _camera == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsRestarting) return;

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
        if (!isModeSwitchPossible)
        {
            isOutOfBounds = rotationX < drivingRotationClampX.x || rotationX > drivingRotationClampX.y ||
                            rotationY < drivingRotationClampY.x || rotationY > drivingRotationClampY.y;
        }

        bool canApplyPlayerInput = !isModeSwitchPossible || !isOutOfBounds;
        if (canApplyPlayerInput)
        {
            rotationX -= lookInput.y;
            rotationY += lookInput.x;
        }

        if (!isModeSwitchPossible)
        {
            if (isOutOfBounds)
            {
                float targetX = rotationX;
                float targetY = rotationY;
                ClampRotationToDrivingBounds(ref targetX, ref targetY);

                rotationX = Mathf.Lerp(rotationX, targetX, Time.deltaTime * snapToBoundsSpeed);
                rotationY = Mathf.Lerp(rotationY, targetY, Time.deltaTime * snapToBoundsSpeed);

                if (Mathf.Abs(rotationX - targetX) < snapCompletionThreshold && Mathf.Abs(rotationY - targetY) < snapCompletionThreshold)
                {
                    rotationX = targetX;
                    rotationY = targetY;
                }
            }
            else
            {
                ClampRotationToDrivingBounds(ref rotationX, ref rotationY);

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
        _camera.transform.localRotation = deviation;
    }

    private void ClampRotationToDrivingBounds(ref float rotX, ref float rotY)
    {
        rotX = Mathf.Clamp(rotX, drivingRotationClampX.x, drivingRotationClampX.y);
        rotY = Mathf.Clamp(rotY, drivingRotationClampY.x, drivingRotationClampY.y);
    }

    private void OnLookPerformed(InputAction.CallbackContext ctx)
    {
        lookInput = ctx.ReadValue<Vector2>();
        if (ctx.control.device is Gamepad)
        {
            lookInput *= gamepadSensitivity;
        }
        else
        {
            lookInput *= sensitivity;
        }
    }

    private void OnLookCanceled(InputAction.CallbackContext ctx)
    {
        lookInput = Vector2.zero;
    }

    void OnEnable()
    {
        if (lookAction != null)
        {
            lookAction.action.Enable();
            lookAction.action.performed += OnLookPerformed;
            lookAction.action.canceled += OnLookCanceled;
        }
    }

    void OnDisable()
    {
        if (lookAction != null)
        {
            lookAction.action.performed -= OnLookPerformed;
            lookAction.action.canceled -= OnLookCanceled;
            lookAction.action.Disable();
        }
    }
}
