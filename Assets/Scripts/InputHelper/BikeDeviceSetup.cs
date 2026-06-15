using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

public class BikeDeviceSetup : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterLayout()
    {
        InputSystem.RegisterLayout<BikeDevice>(
            matches: new InputDeviceMatcher()
                .WithInterface("HID")
                .WithCapability("vendorId", 0xE502)
                .WithCapability("productId", 0xBBAB)
        );

        Debug.Log("BikeDevice layout registered");
    }

    void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    // Log ALL device changes, not just BikeDevice
    static void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        // Debug.Log($"Device change: {device.displayName} | {device.description.interfaceName} | vendorId={device.description.capabilities} | change={change}");

        if (device is BikeDevice && change == InputDeviceChange.Added)
            Debug.Log("Bike device connected as BikeDevice!");
    }
}