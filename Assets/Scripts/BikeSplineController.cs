using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

public class BikeSplineController : MonoBehaviour
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

    private float speed;
    public float CurrentSpeed => speed;
    public bool freezeMovement = false;
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
        cameraController.isDriving = speed > 0.25f;
        if (freezeMovement) return;

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
            speed -= drag * 5 * Time.deltaTime;
        }


        distanceCovered += speed * Time.deltaTime;
        t = distanceCovered / splineLenght;
        currentTangent = spline.Spline.EvaluateTangent(t);
        float nextT = (distanceCovered + 1) / splineLenght;
        forwardTangent = spline.Spline.EvaluateTangent(Mathf.Clamp01(nextT));
        transform.position = (Vector3)spline.Spline.EvaluatePosition(t) + spline.transform.position;
        transform.rotation = Quaternion.LookRotation(currentTangent);
        Drag();
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
        }
        else if (!isRight && lastAccelTimeLeft >= accelerationTimer)
        {
            isAcceleratingLeft = true;
            isLeftAccelHeld = true;
        }
    }
    private void StopAccelerating(bool isRight, bool isCancelled = false)
    {
        if (isRight)
        {
            if (isCancelled)
            {
                isRightAccelHeld = false;
            }
            isAcceleratingRight = false;
        }
        else
        {
            if (isCancelled)
            {
                isLeftAccelHeld = false;
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

    void OnEnable()
    {
        if (rightAction != null && leftAction != null)
        {
            rightAction.action.Enable();
            rightAction.action.started += ctx => Accelerate(true);
            rightAction.action.canceled += ctx => StopAccelerating(true, true);
            rightAction.action.performed += ctx => StopAccelerating(true);
            leftAction.action.Enable();
            leftAction.action.started += ctx => Accelerate(false);
            leftAction.action.canceled += ctx => StopAccelerating(false, true);
            leftAction.action.performed += ctx => StopAccelerating(false);
        }
    }
    void OnDisable()
    {
        if (leftAction != null && rightAction != null)
        {
            rightAction.action.started -= ctx => Accelerate(true);
            rightAction.action.canceled -= ctx => StopAccelerating(true, true);
            rightAction.action.performed -= ctx => StopAccelerating(true);
            leftAction.action.Disable();
            leftAction.action.started -= ctx => Accelerate(false);
            leftAction.action.canceled -= ctx => StopAccelerating(false, true);
            leftAction.action.performed -= ctx => StopAccelerating(false);
            rightAction.action.Disable();
        }
    }
}
