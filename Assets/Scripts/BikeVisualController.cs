using UnityEngine;

public class BikeVisualController : MonoBehaviour
{
    [Header("Bike Parts & Visuals")]
    [SerializeField] private GameObject frontSection;
    [SerializeField] private GameObject frontWheel;
    [SerializeField] private GameObject backWheel;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float wheelSpinMultiplier = 20f;
    [SerializeField] private float maxSteerAngle = 30f;

    [Header("Visual Effects: Lean")]
    [SerializeField] private float maxLeanAngle = 25f;
    [SerializeField] private float leanSmoothness = 8f;
    [SerializeField] private float leanFullEffectSpeed = 15f;

    [Header("Visual Effects: Wiggle")]
    [SerializeField] private float wiggleFrequency = 2f;
    [SerializeField] private float wiggleFrequencySpeedBoost = 1f;
    [SerializeField] private float wiggleMaxAngle = 1.5f;
    [SerializeField] private float wiggleFullEffectSpeed = 15f;

    private Quaternion frontSectionBaseRotation;
    private Quaternion visualRootBaseRotation;
    private float currentLeanAngle;
    private float wigglePhase;

    private void Awake()
    {
        CacheVisualBaseRotations();
    }

    private void CacheVisualBaseRotations()
    {
        if (frontSection != null)
            frontSectionBaseRotation = frontSection.transform.localRotation;

        if (visualRoot != null)
            visualRootBaseRotation = visualRoot.localRotation;
    }

    public void UpdateVisuals(float currentForwardSpeed, float currentSteerInput)
    {
        UpdateFrontSectionVisual(currentSteerInput);
        UpdateVisualLean(currentForwardSpeed, currentSteerInput);
        RotateWheels(currentForwardSpeed);
    }

    private void UpdateFrontSectionVisual(float currentSteerInput)
    {
        if (frontSection == null) return;
        frontSection.transform.localRotation = frontSectionBaseRotation * Quaternion.Euler(0f, 0f, currentSteerInput * maxSteerAngle);
    }

    private void RotateWheels(float currentForwardSpeed)
    {
        float wheelSpin = currentForwardSpeed * Mathf.PI * 2 * wheelSpinMultiplier * Time.deltaTime;

        if (backWheel != null)
            backWheel.transform.Rotate(Vector3.back, wheelSpin, Space.Self);
        if (frontWheel != null)
            frontWheel.transform.Rotate(Vector3.back, wheelSpin, Space.Self);
    }

    private void UpdateVisualLean(float currentForwardSpeed, float currentSteerInput)
    {
        if (visualRoot == null) return;

        float speedMagnitude = Mathf.Abs(currentForwardSpeed);
        float leanSpeedFactor = Mathf.Clamp01(speedMagnitude / Mathf.Max(0.01f, leanFullEffectSpeed));
        float targetLean = currentSteerInput * maxLeanAngle * leanSpeedFactor;

        currentLeanAngle = Mathf.Lerp(currentLeanAngle, targetLean, leanSmoothness * Time.deltaTime);

        float wiggleSpeedFactor = Mathf.Clamp01(speedMagnitude / Mathf.Max(0.01f, wiggleFullEffectSpeed));
        float wiggleFrequencyAtSpeed = wiggleFrequency * (wiggleFrequencySpeedBoost * wiggleSpeedFactor);
        wigglePhase += wiggleFrequencyAtSpeed * Mathf.PI * 2f * Time.deltaTime;

        if (wigglePhase > Mathf.PI * 2f)
            wigglePhase -= Mathf.PI * 2f;

        float wiggleAngle = Mathf.Sin(wigglePhase) * wiggleMaxAngle * wiggleSpeedFactor;
        float totalLean = currentLeanAngle + wiggleAngle;

        visualRoot.localRotation = visualRootBaseRotation * Quaternion.Euler(-totalLean, 0f, 0f);
    }
}