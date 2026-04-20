using UnityEngine;
using UnityEngine.InputSystem;

public class BikeController : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private float groundCheckDistance = 0.25f;

    private Rigidbody rb;

    private bool isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void Start()
    {
        inputActions.FindAction("Move").performed += ctx => Move(ctx.ReadValue<Vector2>());
    }

    private void Move(Vector2 direction)
    {
        rb.linearVelocity = new Vector3(direction.x, rb.linearVelocity.y, direction.y) * 5f;
    }

    private void Update()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundCheckDistance, LayerMask.GetMask("Ground")))
        {
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }
        if (!isGrounded)
        {
            print("Not grounded");
            rb.AddForce(Vector3.down * 9.81f, ForceMode.Acceleration);
        }
        else
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }
}
