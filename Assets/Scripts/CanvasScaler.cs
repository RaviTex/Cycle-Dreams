using UnityEngine;

public class CanvasScaler : MonoBehaviour
{
    private Camera targetCamera;
    private float referenceFOV = 60f;
    private float VRScaleMultiplier = 1f;
    private float referenceFOVVR = 98.04f;
    private bool isInVRMode = false;
    private Vector3 initialScale;


    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        initialScale = transform.localScale;
        isInVRMode = GameManager.Instance.isInVRMode;
        referenceFOV = GameManager.Instance.referenceFOV;
        VRScaleMultiplier = GameManager.Instance.VRScaleMultiplier;
        referenceFOVVR = GameManager.Instance.referenceFOVVR;
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
