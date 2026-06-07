using UnityEngine;
using UnityEngine.InputSystem;

public class BikeController : MonoBehaviour, IBikeController
{
    [Header("Input Setup")]
    public InputActionReference moveAction;

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

    private BikeVisualController visualController;
    private Rigidbody rb;
    private int groundLayer;
    private float currentSteerInput;
    private float currentForwardSpeed;
    private float currentSpeed;
    public float CurrentSpeed => currentSpeed;
    private float currentThrottleInput;
    private bool freezeMovementState = false;
    private bool wasKinematicBeforeFreeze = false;
    public bool freezeMovement
    {
        get => freezeMovementState;
        set
        {
            if (freezeMovementState == value) return;
            freezeMovementState = value;

            if (rb == null) return;

            if (freezeMovementState)
            {
                wasKinematicBeforeFreeze = rb.isKinematic;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            else
            {
                rb.isKinematic = wasKinematicBeforeFreeze;
            }
        }
    }

    void Awake()
    {
        InitializePhysics();
    }

    void OnEnable() { if (moveAction != null) moveAction.action.Enable(); }
    void OnDisable() { if (moveAction != null) moveAction.action.Disable(); }

    private void Start()
    {
        visualController = GameManager.Instance.bikeVisualController;
    }

    void Update()
    {
        if (freezeMovement) return;

        UpdateSteeringInput();
        if (visualController != null)
        {
            visualController.UpdateVisuals(currentForwardSpeed, currentSteerInput);
        }
    }

    void FixedUpdate()
    {
        if (freezeMovement)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
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

    private Vector2 ReadMoveInput()
    {
        if (moveAction == null) return Vector2.zero;
        Vector2 input = moveAction.action.ReadValue<Vector2>();

        if (TeensyReader.Instance != null && TeensyReader.Instance.IsDeviceConnected)
        {
            input.y = 0f; // Block gamepad throttle/brake
        }

        return input;
    }

    private void UpdateSteeringInput()
    {
        float steerInput = ReadMoveInput().x;
        currentSteerInput = Mathf.Lerp(currentSteerInput, steerInput, steeringLerpSpeed * Time.deltaTime);
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
        if (TeensyReader.Instance != null && TeensyReader.Instance.IsDeviceConnected)
        {
            float targetForwardSpeed = TeensyReader.Instance.CurrentSpeed;

            // Raycast forward to check if a wall is in front of us
            bool isWallAhead = Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, out RaycastHit forwardHit, 1.5f, groundLayer);

            // If there's a steep surface in front (normal.y < 0.5 means a slope steeper than 60 degrees) or 
            // the bike itself is angled too sharply upwards, kill the forced speed to prevent wall climbing.
            if ((isWallAhead && forwardHit.normal.y < 0.5f) || Vector3.Angle(Vector3.up, transform.forward) < 45f)
            {
                targetForwardSpeed = 0f;
            }

            // Exactly enforce the target speed on the forward axis to bypass gravity/drag deceleration
            velocity -= transform.forward * currentForwardSpeed;
            velocity += transform.forward * targetForwardSpeed;

            return;
        }

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
}