using UnityEngine;
using UnityEngine.XR.Management;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public bool isInVRMode = false;
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
    }
}
