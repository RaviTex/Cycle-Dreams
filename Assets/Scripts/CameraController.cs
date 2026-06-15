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

    private float rotationX = 0f;
    private float rotationY = 0f;
    private Vector2 lookInput;

    void LateUpdate()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.IsRestarting) return;
        if (gm.cameraPivot == null || gm.mainCamera == null) return;

        Transform pivotTransform = gm.cameraPivot.transform;
        GameObject cameraGO = gm.mainCamera.gameObject;
        bool vrMode = gm.isInVRMode;
        bool modeSwitchPossible = gm.isModeSwitchPossible;

        if (lerpPositionAndRotation)
        {
            transform.position = Vector3.Lerp(transform.position, pivotTransform.position, Time.deltaTime * lerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, pivotTransform.rotation, Time.deltaTime * lerpSpeed);
        }
        else
        {
            transform.position = pivotTransform.position;
            transform.rotation = pivotTransform.rotation;
        }

        if (vrMode)
        {
            return;
        }

        bool isOutOfBounds = false;
        if (!modeSwitchPossible)
        {
            isOutOfBounds = rotationX < drivingRotationClampX.x || rotationX > drivingRotationClampX.y ||
                            rotationY < drivingRotationClampY.x || rotationY > drivingRotationClampY.y;
        }

        bool canApplyPlayerInput = !modeSwitchPossible || !isOutOfBounds;
        if (canApplyPlayerInput)
        {
            rotationX -= lookInput.y;
            rotationY += lookInput.x;
        }

        if (!modeSwitchPossible)
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
        cameraGO.transform.localRotation = deviation;
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
