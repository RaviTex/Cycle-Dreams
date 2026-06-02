using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

public class BikeSplineController : MonoBehaviour, IBikeController
{
    public InputActionReference rightAction;
    public InputActionReference leftAction;
    [SerializeField] private SplineContainer spline;
    [SerializeField] private float acceleration = 1f;
    [SerializeField] private float drag = 1f;
    [SerializeField] private float accelerationTimer = 0.5f;
    [SerializeField] private float maxLeanAngle = 30f;
    [SerializeField] private GameObject bikeModel;
    [SerializeField] private GameObject frontWheel;
    [SerializeField] private GameObject backWheel;
    [SerializeField] private float wheelSpinMultiplier = 1f;
    [SerializeField] private GameObject frontSection;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private float cameraDrivingSpeedThreshold = 0.25f;
    [SerializeField] private float dualButtonBrakeMultiplier = 5f;

    private float speed;
    public float CurrentSpeed => speed;
    private bool freezeMovementState = false;
    public bool freezeMovement
    {
        get => freezeMovementState;
        set
        {
            if (freezeMovementState == value) return;
            freezeMovementState = value;

            if (freezeMovementState)
            {
                speed = 0f;
                isAcceleratingRight = false;
                isAcceleratingLeft = false;
                isRightAccelHeld = false;
                isLeftAccelHeld = false;
            }
        }
    }
    private float splineLenght;
    private float distanceCovered;
    private float t;
    private float lastAccelTimeLeft;
    private float lastAccelTimeRight;
    private bool isAcceleratingRight;
    private bool isAcceleratingLeft;
    private bool isRightAccelHeld;
    private bool isLeftAccelHeld;
    private float leanAngleGoal;
    private Quaternion initalBikeModelRotation;
    private Quaternion currentBikeModelRotation;

    private Vector3 currentTangent;
    private Vector3 forwardTangent;
    private Quaternion initialSteerRotation;

    void Awake()
    {
        if (cameraController == null)
        {
            cameraController = FindFirstObjectByType<CameraController>();
        }
    }

    void Start()
    {
        splineLenght = spline.Spline.GetLength();
        initalBikeModelRotation = bikeModel.transform.localRotation;
        currentBikeModelRotation = initalBikeModelRotation;
        initialSteerRotation = frontSection.transform.localRotation;
        print(splineLenght);
    }

    void Update()
    {
        if (cameraController != null)
        {
            cameraController.isDriving = speed > cameraDrivingSpeedThreshold;
        }

        if (freezeMovement) return;

        if (TeensyReader.Instance != null && TeensyReader.Instance.IsDeviceConnected)
        {
            speed = TeensyReader.Instance.CurrentSpeed;
        }
        else
        {
            if (isAcceleratingRight)
            {
                speed += acceleration * Time.deltaTime;
            }
            if (isAcceleratingLeft)
            {
                speed += acceleration * Time.deltaTime;
            }

            if (isRightAccelHeld && isLeftAccelHeld)
            {
                speed -= drag * dualButtonBrakeMultiplier * Time.deltaTime;
            }

            Drag();
        }

        distanceCovered += speed * Time.deltaTime;
        t = distanceCovered / splineLenght;
        currentTangent = spline.Spline.EvaluateTangent(t);
        float nextT = (distanceCovered + 1) / splineLenght;
        forwardTangent = spline.Spline.EvaluateTangent(Mathf.Clamp01(nextT));
        transform.position = (Vector3)spline.Spline.EvaluatePosition(t) + spline.transform.position;
        transform.rotation = Quaternion.LookRotation(currentTangent);
        lastAccelTimeLeft += Time.deltaTime;
        lastAccelTimeRight += Time.deltaTime;
        Rotation();
        RotateWheels();
        Debug.DrawLine(transform.position, transform.position + currentTangent, Color.red);
        Debug.DrawLine(transform.position, transform.position + forwardTangent, Color.blue);
        Vector3 localForward = transform.InverseTransformDirection(forwardTangent);
        float steerAngle = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;
        frontSection.transform.localRotation = initialSteerRotation * Quaternion.Euler(0, steerAngle, 0);

        // print(speed);
    }

    private void Rotation()
    {
        if (isAcceleratingRight && !isAcceleratingLeft)
        {
            leanAngleGoal = -maxLeanAngle;
        }
        else if (isAcceleratingLeft && !isAcceleratingRight)
        {
            leanAngleGoal = maxLeanAngle;
        }
        else
        {
            leanAngleGoal = 0f;
        }

        currentBikeModelRotation = Quaternion.Lerp(currentBikeModelRotation, initalBikeModelRotation * Quaternion.Euler(leanAngleGoal, 0, 0), Time.deltaTime * 5f);
        bikeModel.transform.localRotation = currentBikeModelRotation;
    }

    private void Accelerate(bool isRight)
    {
        if (isRight && lastAccelTimeRight >= accelerationTimer)
        {
            isAcceleratingRight = true;
            isRightAccelHeld = true;
            lastAccelTimeRight = 0f;
        }
        else if (!isRight && lastAccelTimeLeft >= accelerationTimer)
        {
            isAcceleratingLeft = true;
            isLeftAccelHeld = true;
            lastAccelTimeLeft = 0f;
        }
    }
    private void StopAccelerating(bool isRight, bool isCancelled = false)
    {
        if (isRight)
        {
            if (isCancelled)
            {
                isRightAccelHeld = false;
                lastAccelTimeRight = 0f;
            }
            isAcceleratingRight = false;
        }
        else
        {
            if (isCancelled)
            {
                isLeftAccelHeld = false;
                lastAccelTimeLeft = 0f;
            }
            isAcceleratingLeft = false;
        }
    }

    private void Drag()
    {
        speed -= drag * Time.deltaTime;
        speed = Mathf.Max(speed, 0);
    }

    private void RotateWheels()
    {
        float wheelSpin = speed * Mathf.PI * 2 * wheelSpinMultiplier * Time.deltaTime;

        if (backWheel != null)
            backWheel.transform.Rotate(Vector3.back, wheelSpin, Space.Self);
        if (frontWheel != null)
            frontWheel.transform.Rotate(Vector3.back, wheelSpin, Space.Self);
    }

    private void OnRightStarted(InputAction.CallbackContext ctx) => Accelerate(true);
    private void OnRightCanceled(InputAction.CallbackContext ctx) => StopAccelerating(true, true);
    private void OnRightPerformed(InputAction.CallbackContext ctx) => StopAccelerating(true);
    private void OnLeftStarted(InputAction.CallbackContext ctx) => Accelerate(false);
    private void OnLeftCanceled(InputAction.CallbackContext ctx) => StopAccelerating(false, true);
    private void OnLeftPerformed(InputAction.CallbackContext ctx) => StopAccelerating(false);

    void OnEnable()
    {
        if (rightAction != null && leftAction != null)
        {
            rightAction.action.Enable();
            rightAction.action.started += OnRightStarted;
            rightAction.action.canceled += OnRightCanceled;
            rightAction.action.performed += OnRightPerformed;
            leftAction.action.Enable();
            leftAction.action.started += OnLeftStarted;
            leftAction.action.canceled += OnLeftCanceled;
            leftAction.action.performed += OnLeftPerformed;
        }
    }
    void OnDisable()
    {
        if (leftAction != null && rightAction != null)
        {
            rightAction.action.started -= OnRightStarted;
            rightAction.action.canceled -= OnRightCanceled;
            rightAction.action.performed -= OnRightPerformed;
            rightAction.action.Disable();
            leftAction.action.started -= OnLeftStarted;
            leftAction.action.canceled -= OnLeftCanceled;
            leftAction.action.performed -= OnLeftPerformed;
            leftAction.action.Disable();
        }
    }
}
