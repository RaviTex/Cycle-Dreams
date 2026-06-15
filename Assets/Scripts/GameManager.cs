using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    [Header("Bike Controller Settings")]
    public bool isModeSwitchPossible => GetActiveBikeController()?.CurrentSpeed < modeSwitchSpeedThreshold;
    public float modeSwitchSpeedThreshold = 0.5f;
    [Header("Prototype Animals")]
    public List<GameObject> prototypeAnimals;
    private bool hasFotographedRabbit;
    public bool HasFotographedRabbit
    {
        get => hasFotographedRabbit;
        set
        {
            hasFotographedRabbit = value;
            if (value)
            {
                rabbitCheckmark.SetActive(true);
                rabbitXmark.SetActive(false);
            }
            else
            {
                rabbitCheckmark.SetActive(false);
                rabbitXmark.SetActive(true);
            }
        }
    }
    private bool hasFotographedBear;
    public bool HasFotographedBear
    {
        get => hasFotographedBear;
        set
        {
            hasFotographedBear = value;
            if (value)
            {
                bearCheckmark.SetActive(true);
                bearXmark.SetActive(false);
            }
            else
            {
                bearCheckmark.SetActive(false);
                bearXmark.SetActive(true);
            }
        }
    }
    [SerializeField] private GameObject rabbitCheckmark;
    [SerializeField] private GameObject bearCheckmark;
    [SerializeField] private GameObject rabbitXmark;
    [SerializeField] private GameObject bearXmark;
    public event Action OnMovementFreeze;
    public event Action OnGameOver;

    public bool IsInteractableUIVisible
    {
        get => isInteractableUIVisible;
        set
        {
            isInteractableUIVisible = value;
            if (isInteractableUIVisible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = isInVRMode ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = isInVRMode;
            }
        }
    }
    private bool isInteractableUIVisible;


    public bool IsRestarting { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        isInVRMode = XRGeneralSettings.Instance.Manager.activeLoader != null;
        print("Is VR Device Active: " + isInVRMode);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        IsRestarting = false;
        InitializeReferences();
    }

    void Start()
    {
        InitializeReferences();
    }

    private void InitializeReferences()
    {
        if (bikeController == null || bikeController.gameObject.IsDestroyed())
        {
            bikeController = FindFirstObjectByType<BikeController>();
        }
        if (bikeSplineController == null || bikeSplineController.gameObject.IsDestroyed())
        {
            bikeSplineController = FindFirstObjectByType<BikeSplineController>();
        }
        if (cameraPivot == null || cameraPivot.gameObject.IsDestroyed())
        {
            cameraPivot = FindFirstObjectByType<CameraPivot>();
        }
        if (cameraController == null || cameraController.gameObject.IsDestroyed())
        {
            cameraController = FindFirstObjectByType<CameraController>();
        }
        if (bikeVisualController == null || bikeVisualController.gameObject.IsDestroyed())
        {
            bikeVisualController = FindFirstObjectByType<BikeVisualController>();
        }
        if (mainCamera == null || mainCamera.gameObject.IsDestroyed())
        {
            var connector = FindFirstObjectByType<MainCameraConnector>();
            if (connector != null)
                mainCamera = connector.GetComponent<Camera>();
        }

        if (bikeSplineController != null)
            bikeSplineController.enabled = isSplineMode;
        if (bikeController != null)
            bikeController.enabled = !isSplineMode;

        Cursor.lockState = isInVRMode ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isInVRMode;
    }

    public void PrototypeComplete()
    {
        IsInteractableUIVisible = true;
        print("Prototype Complete!");
        UIManager.Instance.PrototypeComplete();
        FreezeBikeMovement();
    }
    public void GameOver()
    {
        IsInteractableUIVisible = true;
        UIManager.Instance.ShowGameOver();
        FreezeBikeMovement();
    }
    public void RestartGame()
    {
        IsRestarting = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void FreezeBikeMovement()
    {
        var bike = GetActiveBikeController();
        if (bike != null)
        {
            bike.freezeMovement = true;
            OnMovementFreeze?.Invoke();
        }
    }

    public IBikeController GetActiveBikeController()
    {
        if (bikeController != null && bikeController.isActiveAndEnabled)
            return bikeController;
        if (bikeSplineController != null && bikeSplineController.isActiveAndEnabled)
            return bikeSplineController;
        return null;
    }
}
