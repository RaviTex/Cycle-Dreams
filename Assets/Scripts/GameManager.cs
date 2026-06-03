using UnityEngine;
using UnityEngine.XR.Management;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    [Header("Modes")]
    public bool isInVRMode = false;
    public bool isSplineMode = false;
    [Header("Public References")]
    public BikeController bikeController;
    public BikeSplineController bikeSplineController;
    public CameraPivot cameraPivot;
    public CameraController cameraController;
    public BikeVisualController bikeVisualController;
    public Camera mainCamera;
    public Camera photoCamera;
    [Header("Canvas Scaler Settings")]
    [Tooltip("The FOV at which the canvas scale should be unchanged.")]
    public float referenceFOV = 60f;
    public float VRScaleMultiplier = 0.5f;
    public float referenceFOVVR = 98.04f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        isInVRMode = XRGeneralSettings.Instance.Manager.activeLoader != null;
        print("Is VR Device Active: " + isInVRMode);

        if (bikeController == null)
        {
            bikeController = FindFirstObjectByType<BikeController>();
        }
        if (bikeSplineController == null)
        {
            bikeSplineController = FindFirstObjectByType<BikeSplineController>();
        }
        if (cameraPivot == null)
        {
            cameraPivot = FindFirstObjectByType<CameraPivot>();
        }
        if (cameraController == null)
        {
            cameraController = FindFirstObjectByType<CameraController>();
        }
        if (bikeVisualController == null)
        {
            bikeVisualController = FindFirstObjectByType<BikeVisualController>();
        }
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        bikeSplineController.enabled = isSplineMode;
        bikeController.enabled = !isSplineMode;
    }
}
