using UnityEngine;
using UnityEngine.InputSystem;

public class BikeController : MonoBehaviour
{
    [Header("Mode Configuration")]
    public bool useSplineMode = false;

    [Header("Other Controllers")]
    [SerializeField] private CameraController cameraController;

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
    [Tooltip("The absolute maximum speed (m/s) the engine can push the bike. Torque scales down efficiently as you reach this speed.")]
    public float topSpeed = 25f;
    [Tooltip("Active braking force applied when pushing opposite to travel direction. Increase to stop faster.")]
    public float brakingPower = 8f;
    [Tooltip("How fast the gas input registers (higher = instant response). Smoothes out jerky input.")]
    public float throttleResponseSpeed = 2f;
    [Tooltip("The bike must be slower than this speed (m/s) before it will switch from forward to reverse (or vice-versa).")]
    public float reverseEngageSpeedThreshold = 1f;

    [Header("Steering & Handling")]
    [Tooltip("Maximum turn speed when driving slowly.")]
    public float maxTurnSpeed = 40f;
    [Tooltip("Minimum turn speed when driving at high speed.")]
    public float minTurnSpeed = 10f;
    [Tooltip("Speed at which turning reaches the minimum turn speed.")]
    public float maxSpeedForTurn = 25f;
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
    [Tooltip("General air resistance taking away speed exponentially as you go faster.")]
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
    private float currentSpeed;
    public float CurrentSpeed => currentSpeed;
    private float currentThrottleInput;
    private float wigglePhase;
    public bool freezeMovement = false;

    void Awake()
    {
        BikeSplineController splineController = GetComponent<BikeSplineController>();
        if (splineController != null)
        {
            splineController.enabled = useSplineMode;
        }

        if (useSplineMode)
        {
            this.enabled = false;
        }

        InitializePhysics();
        CacheVisualBaseRotations();
    }

    void OnEnable() { if (moveAction != null) moveAction.action.Enable(); }
    void OnDisable() { if (moveAction != null) moveAction.action.Disable(); }

    void Update()
    {
        if (freezeMovement) return;

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
        cameraController.isDriving = currentSpeed > 0.25f;
        if (freezeMovement)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        Vector2 input = ReadMoveInput();
        Vector3 currentVelocity = rb.linearVelocity;
        currentSpeed = currentVelocity.magnitude;
        currentForwardSpeed = Vector3.Dot(currentVelocity, transform.forward);

        float smoothedThrottle = SmoothThrottleInput(input.y);
        Quaternion currentRotation = rb.rotation;

        HandleGrounding(ref currentVelocity, ref currentRotation);
        ApplyMotorAndBraking(ref currentVelocity, input.y, smoothedThrottle);
        ApplyHorizontalMotionCorrection(ref currentVelocity);
        RotateBikeBody(ref currentRotation);

        rb.linearVelocity = currentVelocity;
        rb.MoveRotation(currentRotation);
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

    private void HandleGrounding(ref Vector3 velocity, ref Quaternion rotation)
    {
        if (frontWheelCenter == null || backWheelCenter == null)
            return;

        bool frontGrounded = Physics.Raycast(frontWheelCenter.transform.position, Vector3.down, out RaycastHit frontHit, groundCheckDistance, groundLayer);
        bool backGrounded = Physics.Raycast(backWheelCenter.transform.position, Vector3.down, out RaycastHit backHit, groundCheckDistance, groundLayer);

        Vector3 averageNormal = Vector3.zero;
        float averageError = 0f;
        int groundedCount = 0;

        if (frontGrounded)
        {
            averageNormal += frontHit.normal;
            averageError += (targetRideHeight - frontHit.distance);
            groundedCount++;
        }
        if (backGrounded)
        {
            averageNormal += backHit.normal;
            averageError += (targetRideHeight - backHit.distance);
            groundedCount++;
        }

        if (groundedCount > 0)
        {
            averageNormal = (averageNormal / groundedCount).normalized;
            averageError /= groundedCount;

            float velocityAlongNormal = Vector3.Dot(velocity, averageNormal);
            float upwardVelocityChange = (averageError * suspensionSpringStrength * Time.fixedDeltaTime) - (velocityAlongNormal * suspensionDamper * Time.fixedDeltaTime);

            velocity += averageNormal * upwardVelocityChange;

            Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, averageNormal).normalized;
            if (projectedForward.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(projectedForward, Vector3.up);
                rotation = Quaternion.Slerp(rotation, targetRot, 10f * Time.fixedDeltaTime);
            }
        }

        velocity += Vector3.down * gravity * Time.fixedDeltaTime;
    }

    private void ApplyMotorAndBraking(ref Vector3 velocity, float rawInputY, float smoothedThrottle)
    {
        bool hasDirectionalInput = Mathf.Abs(rawInputY) > 0.01f;
        bool isMoving = Mathf.Abs(currentForwardSpeed) > reverseEngageSpeedThreshold;
        bool isOppositeDirection = hasDirectionalInput && Mathf.Sign(rawInputY) != Mathf.Sign(currentForwardSpeed);

        bool isBraking = isMoving && isOppositeDirection;

        if (isBraking)
        {
            float brakeDirection = -Mathf.Sign(currentForwardSpeed);
            velocity += transform.forward * brakingPower * Mathf.Abs(rawInputY) * brakeDirection * Time.fixedDeltaTime;
        }
        else
        {
            // Calculate how close we are to top speed. We fade engine power out as we reach top speed.
            float speedRatio = Mathf.Clamp01(Mathf.Abs(currentForwardSpeed) / topSpeed);
            // Gives full torque at 0 speed, and 0 torque at max speed.
            float availableTorque = 1f - speedRatio;

            velocity += transform.forward * engineAcceleration * smoothedThrottle * availableTorque * Time.fixedDeltaTime;
        }
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

    private void ApplyHorizontalMotionCorrection(ref Vector3 velocity)
    {
        Vector3 wheelForward = CalculateWheelForward();
        Vector3 wheelRight = Vector3.Cross(Vector3.up, wheelForward).normalized;

        float forwardSpeed = Vector3.Dot(velocity, wheelForward);
        float lateralSpeed = Vector3.Dot(velocity, wheelRight);

        // Standard aerodynamic drag (using velocity squared for realistic air resistance)
        float aeroDragVelocity = forwardSpeed * Mathf.Abs(forwardSpeed) * drag * forwardDragMultiplier * 0.01f * Time.fixedDeltaTime;

        // Tire grip: cancel out only the lateral slide, leaving gravity to freely affect forward/backward rolling
        float lateralCorrectionAmount = lateralSpeed * tireGripStrength * Time.fixedDeltaTime;
        if (Mathf.Abs(lateralCorrectionAmount) > Mathf.Abs(lateralSpeed))
            lateralCorrectionAmount = lateralSpeed; // Prevent overcorrection

        velocity -= wheelRight * lateralCorrectionAmount;
    }

    private Vector3 CalculateWheelForward()
    {
        Vector3 wheelForward = Quaternion.Euler(0f, currentSteerInput * maxSteerAngle, 0f) * transform.forward;
        wheelForward = Vector3.ProjectOnPlane(wheelForward, Vector3.up).normalized;

        if (wheelForward.sqrMagnitude < 0.0001f)
            return transform.forward;

        return wheelForward;
    }

    private void RotateBikeBody(ref Quaternion rotation)
    {
        float speedRatio = Mathf.Clamp01(Mathf.Abs(currentForwardSpeed) / maxSpeedForTurn);
        float currentTurnSpeed = Mathf.Lerp(maxTurnSpeed, minTurnSpeed, speedRatio);
        float turnAmount = currentTurnSpeed * currentSteerInput * currentForwardSpeed * Time.fixedDeltaTime;
        rotation *= Quaternion.Euler(0f, turnAmount, 0f);
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