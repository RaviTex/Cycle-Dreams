using UnityEngine;
using UnityEngine.InputSystem;

public class TeensyReader : MonoBehaviour
{
    [Header("Device Settings")]
    public string deviceName = "Teensy";

    [Header("Bike Specifications")]
    [Tooltip("Circumference of the wheel in meters.")]
    public float wheelCircumference = 2.2f;
    [Tooltip("Number of magnets attached to the wheel.")]
    public int magnetsPerWheel = 4;

    [Header("Output Settings")]
    [Tooltip("Smoothing factor for speed output.")]
    public float speedSmoothing = 5f;

    public bool IsDeviceConnected => teensyJoystick != null;
    public float CurrentSpeed { get; private set; }

    private Joystick teensyJoystick;
    private bool lastPassState = false;
    private float timeSinceLastPass = 0f;
    private float lastCalculatedSpeed = 0f;

    // Singleton pattern for easy access
    public static TeensyReader Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        FindDevice();
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    void OnDestroy()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void FindDevice()
    {
        teensyJoystick = null;
        foreach (var device in InputSystem.devices)
        {
            if (device is Joystick joy && device.displayName.Contains(deviceName))
            {
                teensyJoystick = joy;
                Debug.Log($"Connected to device: {device.displayName}");
                break;
            }
        }
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed)
        {
            FindDevice();
        }
    }

    void Update()
    {
        if (!IsDeviceConnected)
        {
            CurrentSpeed = 0f;
            return;
        }

        Vector2 stick = teensyJoystick.stick.ReadValue();
        bool isPassing = stick.x < 0;

        timeSinceLastPass += Time.deltaTime;

        // Detect transition into pass state
        if (isPassing && !lastPassState)
        {
            float distancePerPass = wheelCircumference / magnetsPerWheel;

            // Prevent division by zero and overly high speeds from double-reads
            if (timeSinceLastPass > 0.05f)
            {
                lastCalculatedSpeed = distancePerPass / timeSinceLastPass;
            }
            timeSinceLastPass = 0f;
        }

        lastPassState = isPassing;

        // Decay logic: The speed cannot physically be faster than the time it's currently taking for the next pass
        float distance = wheelCircumference / magnetsPerWheel;
        // If timeSinceLastPass is 0 (exactly on the frame of a pass), maxPossibleSpeed should just allow the last calculated speed
        float maxPossibleSpeed = timeSinceLastPass > 0.001f ? distance / timeSinceLastPass : lastCalculatedSpeed;

        // Hard stop threshold to prevent infinite asymptote. 
        if (maxPossibleSpeed < 0.2f)
        {
            maxPossibleSpeed = 0f;
            lastCalculatedSpeed = 0f;
        }

        float targetSpeed = Mathf.Min(lastCalculatedSpeed, maxPossibleSpeed);

        CurrentSpeed = Mathf.Lerp(CurrentSpeed, targetSpeed, speedSmoothing * Time.deltaTime);

        // Snap directly to 0 if we're extremely close, to prevent endless Lerping
        if (CurrentSpeed < 0.05f)
        {
            CurrentSpeed = 0f;
        }
        print($"Current Speed: {CurrentSpeed:F2} m/s");
    }
}

