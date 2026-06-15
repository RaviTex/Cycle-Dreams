using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

public class TeensyReader : MonoBehaviour
{
    public InputActionReference slider;
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
        // FindDevice();
        // InputSystem.onDeviceChange += OnDeviceChange;
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    void OnDestroy()
    {
        // InputSystem.onDeviceChange -= OnDeviceChange;
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

    static void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (!(device is BikeDevice)) return;

        // Debug.Log($"Device stateBlock format: {device.stateBlock.format}");
        // Debug.Log($"Device stateBlock sizeInBits: {device.stateBlock.sizeInBits}");
        // Debug.Log($"Device description: {device.description.ToJson()}");
        // if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed)
        // {
        //     FindDevice();
        // }
    }

    void Update()
    {
        // if (!IsDeviceConnected)
        // {
        //     CurrentSpeed = 0f;
        //     return;
        // }

        // float sliderLeft = slider.action.ReadValue<float>();

        // float throttle = (sliderLeft + 1f) * 0.5f;
        // float bikeSpeedMS = throttle * 15f;


        // CurrentSpeed = Mathf.Lerp(CurrentSpeed, bikeSpeedMS, speedSmoothing * Time.deltaTime);
        // Debug.Log(
        //     $"Slider={sliderLeft:F3}, " +
        //     $"Throttle={throttle:F3}, " +
        //     $"Speed={bikeSpeedMS:F3}"
        // );        

        if (BikeDevice.current == null) return;

        float normalized = BikeDevice.current.speed.ReadValue();
        float speedMS = normalized * 15.0f;
        Debug.Log($"Speed: {speedMS:F2} m/s");
    }
}

