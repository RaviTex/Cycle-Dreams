using UnityEngine;
using UnityEngine.InputSystem;

public class BikeController : MonoBehaviour
{
    [Header("Input Setup")]
    public InputActionReference moveAction;

    [Header("Bike Parts & Visuals")]
    [SerializeField] private GameObject frontSection;
    [SerializeField] private GameObject frontWheel;
    [SerializeField] private GameObject backWheel;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float wheelSpinMultiplier = 20f;
    [SerializeField] private bool enableVisuals = true;

    [Header("Motor & Braking")]
    [Tooltip("Maximum forward acceleration force.")]
    public float engineAcceleration = 5f;
    [Tooltip("Active braking force applied when pushing opposite to travel direction. Increase to stop faster.")]
    public float brakingPower = 8f;
    [Tooltip("How fast the gas input registers (higher = instant response). Smoothes out jerky input.")]
    public float throttleResponseSpeed = 2f;
    [Tooltip("The bike must be slower than this speed (m/s) before it will switch from forward to reverse (or vice-versa).")]
    public float reverseEngageSpeedThreshold = 1f;

    [Header("Steering & Handling")]
    [Tooltip("How fast the bike turns physically.")]
    public float turnSpeed = 30f;
    [Tooltip("Maximum steering angle for the front wheel visual and steering calculation.")]
    public float maxSteerAngle = 30f;
    [Tooltip("How fast the front wheel turns to the target angle.")]
    public float steeringLerpSpeed = 8f;
    [Tooltip("How strongly the tire grips the road to move in the steering direction.")]
    public float tireGripStrength = 12f;
    [Tooltip("How much speed is lost during a slide/drift (0 = keep all speed, 1 = total speed loss).")]
    public float driftingSpeedLoss = 0.1f;

    [Header("Physics & Friction")]
    public float gravity = 9.81f;
    [Tooltip("General air resistance taking away speed over time.")]
    public float drag = 1f;
    [Tooltip("Modifier for drag when moving purely forward (simulates aerodynamics).")]
    public float forwardDragMultiplier = .7f;

    [Header("Suspension (Grounding)")]
    public float groundCheckDistance = 0.6f;
    public float targetRideHeight = 0.42f;
    public float suspensionSpringStrength = 200f;
    public float suspensionDamper = 15f;
    [SerializeField] private GameObject frontWheelCenter;
    [SerializeField] private GameObject backWheelCenter;

    [Header("Visual Effects: Lean")]
    [SerializeField] private float maxLeanAngle = 25f;
    [SerializeField] private float leanSmoothness = 8f;
    [SerializeField] private float leanFullEffectSpeed = 15f;

    [Header("Visual Effects: Wiggle")]
    [SerializeField] private float wiggleFrequency = 2f;
    [SerializeField] private float wiggleFrequencySpeedBoost = 1f;
    [SerializeField] private float wiggleMaxAngle = 1.5f;
    [SerializeField] private float wiggleFullEffectSpeed = 15f;

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
        if (enableVisuals)
        {
            UpdateFrontSectionVisual();
            UpdateVisualLean();
            RotateWheels();
        }
    }

    void FixedUpdate()
    {
        Vector2 input = ReadMoveInput();
        currentForwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        float smoothedThrottle = SmoothThrottleInput(input.y);

        HandleGrounding();
        ApplyMotorAndBraking(input.y, smoothedThrottle);
        ApplyHorizontalMotionCorrection(smoothedThrottle);
        RotateBikeBody();
        KeepBikeUpright();
    }

    private void KeepBikeUpright()
    {
        Vector3 currentEuler = transform.eulerAngles;
        transform.eulerAngles = new Vector3(currentEuler.x, currentEuler.y, 0f);
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

        frontSection.transform.localRotation = frontSectionBaseRotation * Quaternion.Euler(0f, 0f, currentSteerInput * maxSteerAngle);
    }

    private void HandleGrounding()
    {
        if (frontWheelCenter == null || backWheelCenter == null)
            return;

        rb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);

        bool frontGrounded = ApplyWheelSuspension(frontWheelCenter.transform.position);
        bool backGrounded = ApplyWheelSuspension(backWheelCenter.transform.position);

    }

    private bool ApplyWheelSuspension(Vector3 wheelPosition)
    {
        if (Physics.Raycast(wheelPosition, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer))
        {
            float error = targetRideHeight - hit.distance;
            float velocityAlongNormal = Vector3.Dot(rb.GetPointVelocity(wheelPosition), hit.normal);
            float upwardForce = (error * suspensionSpringStrength) - (velocityAlongNormal * suspensionDamper);

            // Push along the ground's normal instead of global up, this creates natural sliding down slopes
            rb.AddForceAtPosition(hit.normal * upwardForce, wheelPosition, ForceMode.Acceleration);
            return true;
        }
        return false;
    }

    private void ApplyMotorAndBraking(float rawInputY, float smoothedThrottle)
    {
        bool hasDirectionalInput = Mathf.Abs(rawInputY) > 0.01f;
        bool isMoving = Mathf.Abs(currentForwardSpeed) > reverseEngageSpeedThreshold;
        bool isOppositeDirection = hasDirectionalInput && Mathf.Sign(rawInputY) != Mathf.Sign(currentForwardSpeed);

        bool isBraking = isMoving && isOppositeDirection;

        if (isBraking)
        {
            float brakeDirection = -Mathf.Sign(currentForwardSpeed);
            rb.AddForce(transform.forward * brakingPower * Mathf.Abs(rawInputY) * brakeDirection, ForceMode.Acceleration);
        }

        rb.AddForce(transform.forward * engineAcceleration * smoothedThrottle, ForceMode.Acceleration);
    }

    private float SmoothThrottleInput(float targetThrottle)
    {
        targetThrottle = Mathf.Clamp(targetThrottle, -1f, 1f);

        bool hasDirectionalInput = Mathf.Abs(targetThrottle) > 0.01f;
        bool isMoving = Mathf.Abs(currentForwardSpeed) > reverseEngageSpeedThreshold;
        bool isOppositeDirection = hasDirectionalInput && Mathf.Sign(targetThrottle) != Mathf.Sign(currentForwardSpeed);

        bool shouldBrakeToStop = isMoving && isOppositeDirection;

        float desiredThrottle = shouldBrakeToStop ? 0f : targetThrottle;

        float rampPerStep = throttleResponseSpeed * Time.fixedDeltaTime;
        currentThrottleInput = Mathf.MoveTowards(currentThrottleInput, desiredThrottle, rampPerStep);
        return currentThrottleInput;
    }

    private void ApplyHorizontalMotionCorrection(float throttleInput)
    {
        Vector3 wheelForward = CalculateWheelForward();
        Vector3 wheelRight = Vector3.Cross(Vector3.up, wheelForward).normalized;

        float forwardSpeed = Vector3.Dot(rb.linearVelocity, wheelForward);
        float lateralSpeed = Vector3.Dot(rb.linearVelocity, wheelRight);

        // Standard aerodynamic drag (using velocity squared for realistic air resistance)
        float aeroDragForce = forwardSpeed * Mathf.Abs(forwardSpeed) * drag * forwardDragMultiplier * 0.007f;
        rb.AddForce(-wheelForward * aeroDragForce, ForceMode.Acceleration);

        // Optional: reduce forward speed when drifting
        // float driftDrag = Mathf.Abs(lateralSpeed) * driftingSpeedLoss;
        // rb.AddForce(-wheelForward * Mathf.Sign(forwardSpeed) * driftDrag, ForceMode.Acceleration);

        // Tire grip: cancel out only the lateral slide, leaving gravity to freely affect forward/backward rolling
        Vector3 lateralCorrection = -wheelRight * lateralSpeed;
        rb.AddForce(lateralCorrection * tireGripStrength, ForceMode.Acceleration);
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
        float turnAmount = turnSpeed * currentSteerInput * currentForwardSpeed * Time.fixedDeltaTime;
        Vector3 turnTorque = new Vector3(0, turnAmount / Time.fixedDeltaTime, 0);
        rb.angularVelocity = new Vector3(rb.angularVelocity.x, turnTorque.y, rb.angularVelocity.z);
    }

    private void RotateWheels()
    {
        float wheelSpin = currentForwardSpeed * Mathf.PI * 2 * wheelSpinMultiplier * Time.deltaTime;

        if (backWheel != null)
            backWheel.transform.Rotate(Vector3.back, wheelSpin, Space.Self);
        if (frontWheel != null)
            frontWheel.transform.Rotate(Vector3.back, wheelSpin, Space.Self);
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
        visualRoot.localRotation = visualRootBaseRotation * Quaternion.Euler(-totalLean, 0f, 0f);
    }
}