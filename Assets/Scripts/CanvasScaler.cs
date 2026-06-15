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

        var gm = GameManager.Instance;
        if (gm == null) return;

        initialScale = transform.localScale;
        isInVRMode = gm.isInVRMode;
        referenceFOV = gm.referenceFOV;
        VRScaleMultiplier = gm.VRScaleMultiplier;
        referenceFOVVR = gm.referenceFOVVR;
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
