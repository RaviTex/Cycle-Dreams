using UnityEngine;
using UnityEngine.InputSystem;
public class BikeController : MonoBehaviour
{
    public float acceleration = 20f;
    public float turnSpeed = 50f;
    public float gravity = 9.81f;
    public float groundCheckDistance = 1f;
    public float drag = 2f;
    public float maxSteerAngle = 30f;
    public float steeringLerpSpeed = 8f;
    public float wheelSpinMultiplier = 360f;
    public InputActionReference moveAction;
    [SerializeField] private GameObject backWheel;
    [SerializeField] private GameObject frontWheel;
    [SerializeField] private GameObject frontSection;
    private Rigidbody rb; private int groundLayer;
    private Quaternion frontSectionBaseRotation;
    private float currentSteerInput;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        groundLayer = LayerMask.GetMask("Ground");
        if (frontSection != null)
            frontSectionBaseRotation = frontSection.transform.localRotation;
    }
    void OnEnable() { if (moveAction != null) moveAction.action.Enable(); }
    void OnDisable() { if (moveAction != null) moveAction.action.Disable(); }
    void Update()
    {
        float steerInput = moveAction != null ? moveAction.action.ReadValue<Vector2>().x : 0f;
        currentSteerInput = Mathf.Lerp(currentSteerInput, steerInput, steeringLerpSpeed * Time.deltaTime);
        if (frontSection != null)
            frontSection.transform.localRotation = frontSectionBaseRotation * Quaternion.Euler(0f, currentSteerInput * maxSteerAngle, 0f);
    }
    void FixedUpdate()
    {
        Vector2 input = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer))
        {
            Vector3 vel = rb.linearVelocity;
            if (vel.y < 0) { rb.linearVelocity = new Vector3(vel.x, 0f, vel.z); }
            transform.position = new Vector3(transform.position.x, hit.point.y + groundCheckDistance, transform.position.z);
        }
        else
        {
            rb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
        }
        rb.AddForce(transform.forward * acceleration * input.y, ForceMode.Acceleration);
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(-horizontalVelocity * drag, ForceMode.Acceleration);
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float turnAmount = turnSpeed * currentSteerInput * forwardSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turnAmount, 0f));
        float wheelSpin = forwardSpeed * wheelSpinMultiplier * Time.fixedDeltaTime;
        if (backWheel != null) backWheel.transform.Rotate(Vector3.forward, wheelSpin, Space.Self);
        if (frontWheel != null) frontWheel.transform.Rotate(Vector3.forward, wheelSpin, Space.Self);
    }
}