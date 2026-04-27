using UnityEngine;
using UnityEngine.InputSystem;

public class BikeController : MonoBehaviour
{
    public float acceleration = 20f;
    public float turnSpeed = 50f;
    public float gravity = 9.81f;
    public float groundCheckDistance = 1f;
    public float drag = 2f;
    public float forwardDragMultiplier = 0.1f;
    public float lateralToForwardLoss = 0.2f;
    public float lateralReprojectionStrength = 12f;
    public float maxSteerAngle = 30f;
    public float steeringLerpSpeed = 8f;
    public float wheelSpinMultiplier = 360f;
    public InputActionReference moveAction;

    [SerializeField] private GameObject backWheel;
    [SerializeField] private GameObject frontWheel;
    [SerializeField] private GameObject frontSection;
    [SerializeField] private Transform visualRoot;
    [Header("Lean Effect")]
    [SerializeField] private float maxLeanAngle = 15f;
    [SerializeField] private float leanSmoothness = 8f;
    [SerializeField] private float leanFullEffectSpeed = 20f;
    [Header("Wiggle Effect")]
    [SerializeField] private float wiggleFrequency = 4f;
    [SerializeField] private float wiggleFrequencySpeedBoost = 3f;
    [SerializeField] private float wiggleMaxAngle = 1.5f;
    [SerializeField] private float wiggleFullEffectSpeed = 15f;
    [Header("Throttle Control")]
    [SerializeField] private float throttleAccelerationRate = 0.5f;
    [SerializeField] private float throttleBrakeRate = 1.5f;
    [SerializeField] private float reverseEngageSpeedThreshold = 1f;

    private Rigidbody rb;
    private int groundLayer;
    private Quaternion frontSectionBaseRotation;
    private Quaternion visualRootBaseRotation;
    private float currentSteerInput;
    private float currentLeanAngle;
    private float currentForwardSpeed;
    private float currentThrottleInput;
    private float wigglePhase;

    void Awake()
    {
        InitializePhysics();
        CacheVisualBaseRotations();
    }

    void OnEnable() { if (moveAction != null) moveAction.action.Enable(); }
    void OnDisable() { if (moveAction != null) moveAction.action.Disable(); }

    void Update()
    {
        UpdateSteeringInput();
        UpdateFrontSectionVisual();
        UpdateVisualLean();
    }

    void FixedUpdate()
    {
        Vector2 input = ReadMoveInput();
        currentForwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float smoothedThrottle = UpdateThrottleInput(input.y);

        HandleGrounding();
        ApplyForwardAcceleration(smoothedThrottle);
        ApplyHorizontalMotionCorrection(smoothedThrottle);
        RotateBikeBody();
        RotateWheels();
    }

    private void InitializePhysics()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        groundLayer = LayerMask.GetMask("Ground");
    }

    private void CacheVisualBaseRotations()
    {
        if (frontSection != null)
            frontSectionBaseRotation = frontSection.transform.localRotation;

        if (visualRoot != null)
            visualRootBaseRotation = visualRoot.localRotation;
    }

    private Vector2 ReadMoveInput()
    {
        return moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
    }

    private void UpdateSteeringInput()
    {
        float steerInput = ReadMoveInput().x;
        currentSteerInput = Mathf.Lerp(currentSteerInput, steerInput, steeringLerpSpeed * Time.deltaTime);
    }

    private void UpdateFrontSectionVisual()
    {
        if (frontSection == null)
            return;

        frontSection.transform.localRotation = frontSectionBaseRotation * Quaternion.Euler(0f, currentSteerInput * maxSteerAngle, 0f);
    }

    private void HandleGrounding()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer))
        {
            ClampDownwardVelocity();
            SnapToGround(hit.point.y);
            return;
        }

        rb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
    }

    private void ClampDownwardVelocity()
    {
        Vector3 velocity = rb.linearVelocity;
        if (velocity.y < 0f)
            rb.linearVelocity = new Vector3(velocity.x, 0f, velocity.z);
    }

    private void SnapToGround(float groundY)
    {
        transform.position = new Vector3(transform.position.x, groundY + groundCheckDistance, transform.position.z);
    }

    private void ApplyForwardAcceleration(float throttleInput)
    {
        rb.AddForce(transform.forward * acceleration * throttleInput, ForceMode.Acceleration);
    }

    private float UpdateThrottleInput(float targetThrottle)
    {
        targetThrottle = Mathf.Clamp(targetThrottle, -1f, 1f);

        bool hasDirectionalInput = Mathf.Abs(targetThrottle) > 0.01f;
        bool isMoving = Mathf.Abs(currentForwardSpeed) > reverseEngageSpeedThreshold;
        bool isOppositeDirection = hasDirectionalInput && Mathf.Sign(targetThrottle) != Mathf.Sign(currentForwardSpeed);
        bool shouldBrakeToStop = isMoving && isOppositeDirection;

        float desiredThrottle = shouldBrakeToStop ? 0f : targetThrottle;

        bool isReducingThrottle = Mathf.Abs(desiredThrottle) < Mathf.Abs(currentThrottleInput);
        bool isCrossingDirection = Mathf.Sign(desiredThrottle) != Mathf.Sign(currentThrottleInput) && Mathf.Abs(currentThrottleInput) > 0.01f;
        float rate = (isReducingThrottle || isCrossingDirection || shouldBrakeToStop)
            ? throttleBrakeRate
            : throttleAccelerationRate;

        float rampPerStep = Mathf.Max(0f, rate) * Time.fixedDeltaTime;
        currentThrottleInput = Mathf.MoveTowards(currentThrottleInput, desiredThrottle, rampPerStep);
        return currentThrottleInput;
    }

    private void ApplyHorizontalMotionCorrection(float throttleInput)
    {
        Vector3 horizontalVelocity = GetHorizontalVelocity(rb.linearVelocity);
        Vector3 wheelForward = CalculateWheelForward();

        float forwardSpeedOnWheel = Vector3.Dot(horizontalVelocity, wheelForward);
        Vector3 forwardVelocity = wheelForward * forwardSpeedOnWheel;
        Vector3 lateralVelocity = horizontalVelocity - forwardVelocity;

        rb.AddForce(-forwardVelocity * drag * forwardDragMultiplier, ForceMode.Acceleration);

        float horizontalSpeed = horizontalVelocity.magnitude;
        if (horizontalSpeed <= 0.001f)
            return;

        float lossFactor = Mathf.Clamp01(lateralToForwardLoss);
        float lateralRatio = lateralVelocity.magnitude / horizontalSpeed;
        float retainedSpeed = horizontalSpeed * (1f - lateralRatio * lossFactor);

        float preferredTravelSign = Mathf.Sign(forwardSpeedOnWheel);
        if (Mathf.Abs(throttleInput) > 0.05f)
            preferredTravelSign = Mathf.Sign(throttleInput);
        if (preferredTravelSign == 0f)
            preferredTravelSign = 1f;

        Vector3 targetHorizontalVelocity = wheelForward * retainedSpeed * preferredTravelSign;
        Vector3 horizontalCorrection = targetHorizontalVelocity - horizontalVelocity;
        rb.AddForce(horizontalCorrection * lateralReprojectionStrength, ForceMode.Acceleration);
    }

    private Vector3 GetHorizontalVelocity(Vector3 velocity)
    {
        return new Vector3(velocity.x, 0f, velocity.z);
    }

    private Vector3 CalculateWheelForward()
    {
        Vector3 wheelForward = Quaternion.Euler(0f, currentSteerInput * maxSteerAngle, 0f) * transform.forward;
        wheelForward = Vector3.ProjectOnPlane(wheelForward, Vector3.up).normalized;

        if (wheelForward.sqrMagnitude < 0.0001f)
            return transform.forward;

        return wheelForward;
    }

    private void RotateBikeBody()
    {
        currentForwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float turnAmount = turnSpeed * currentSteerInput * currentForwardSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turnAmount, 0f));
    }

    private void RotateWheels()
    {
        float wheelSpin = currentForwardSpeed * wheelSpinMultiplier * Time.fixedDeltaTime;

        if (backWheel != null)
            backWheel.transform.Rotate(Vector3.forward, wheelSpin, Space.Self);
        if (frontWheel != null)
            frontWheel.transform.Rotate(Vector3.forward, wheelSpin, Space.Self);
    }

    private void UpdateVisualLean()
    {
        if (visualRoot == null)
            return;

        float speedMagnitude = Mathf.Abs(currentForwardSpeed);
        float leanSpeedFactor = Mathf.Clamp01(speedMagnitude / Mathf.Max(0.01f, leanFullEffectSpeed));
        float targetLean = currentSteerInput * maxLeanAngle * leanSpeedFactor;
        currentLeanAngle = Mathf.Lerp(currentLeanAngle, targetLean, leanSmoothness * Time.deltaTime);

        float wiggleSpeedFactor = Mathf.Clamp01(speedMagnitude / Mathf.Max(0.01f, wiggleFullEffectSpeed));
        float wiggleFrequencyAtSpeed = wiggleFrequency * (wiggleFrequencySpeedBoost * wiggleSpeedFactor);
        wigglePhase += wiggleFrequencyAtSpeed * Mathf.PI * 2f * Time.deltaTime;
        if (wigglePhase > Mathf.PI * 2f)
            wigglePhase -= Mathf.PI * 2f;
        float wiggleAngle = Mathf.Sin(wigglePhase) * wiggleMaxAngle * wiggleSpeedFactor;

        float totalLean = currentLeanAngle + wiggleAngle;
        visualRoot.localRotation = visualRootBaseRotation * Quaternion.Euler(totalLean, 0f, 0f);
    }
}