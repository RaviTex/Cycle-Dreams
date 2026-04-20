using UnityEngine;
using UnityEngine.InputSystem;

public class BikeController : MonoBehaviour
{
    public float acceleration = 20f;
    public float turnSpeed = 50f;
    public float gravity = 9.81f;
    public float groundCheckDistance = 1f;
    public float drag = 2f;

    public InputActionReference moveAction;
    private Rigidbody rb;
    private int groundLayer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        groundLayer = LayerMask.GetMask("Ground");
    }

    void OnEnable()
    {
        if (moveAction != null)
            moveAction.action.Enable();
    }

    void OnDisable()
    {
        if (moveAction != null)
            moveAction.action.Disable();
    }

    void FixedUpdate()
    {
        Vector2 input = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer))
        {
            Vector3 vel = rb.linearVelocity;
            if (vel.y < 0)
            {
                rb.linearVelocity = new Vector3(vel.x, 0f, vel.z);
            }

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
        transform.Rotate(Vector3.up, turnSpeed * input.x * forwardSpeed * Time.fixedDeltaTime);
    }
}
