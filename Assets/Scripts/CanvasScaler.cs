using UnityEngine;

public class CanvasScaler : MonoBehaviour
{
    [Header("References")]
    public Camera targetCamera;

    [Header("FOV Settings")]
    [Tooltip("The FOV at which the canvas scale should be unchanged.")]
    public bool isInVRMode = false;
    public float VRScaleMultiplier = 1f;
    public float referenceFOVVR = 98.04f;
    public float referenceFOV = 60f;

    private Vector3 initialScale;

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        initialScale = transform.localScale;
        isInVRMode = GameManager.Instance.isInVRMode;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            return;

        float scaleMultiplier =
            Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) /
            Mathf.Tan(isInVRMode ? referenceFOVVR : referenceFOV * 0.5f * Mathf.Deg2Rad);

        transform.localScale = initialScale * scaleMultiplier * (isInVRMode ? VRScaleMultiplier : 1f);
    }
}
