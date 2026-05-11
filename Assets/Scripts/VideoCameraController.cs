using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class VideoCameraController : MonoBehaviour
{
    public InputActionReference cameraModeToggleAction;
    public InputActionReference viewPhotoModeToggleAction;
    public InputActionReference lefMouseClickAction;
    public InputActionReference scrollWheelAction;
    public bool isInCameraMode = false;
    public bool isInViewPhotoMode = false;
    public GameObject isInCameraModeIndicator;
    public GameObject isRecordingIndicator;
    public GameObject photoGalleryPanel;
    [SerializeField] private TMP_Text pageCountText;
    [SerializeField] private float maxZoomInFOV = 20f;
    [SerializeField] private float maxZoomOutFOV = 100f;
    public int resWidth = 1920;
    public int resHeight = 1080;
    public List<Sprite> shotImages = new List<Sprite>();
    public List<Image> photoDisplays = new List<Image>();

    private Camera mainCamera;
    private RenderTexture renderTexture;
    private float normalFOV;
    private int currentPhotoStartIndex = 0;

    void Start()
    {
        mainCamera = Camera.main;
        normalFOV = mainCamera.fieldOfView;
        renderTexture = new RenderTexture(resWidth, resHeight, 24);
    }

    void Update()
    {
        if (cameraModeToggleAction.action.triggered && !isInViewPhotoMode)
        {
            isInCameraMode = !isInCameraMode;
            isInCameraModeIndicator.SetActive(isInCameraMode);
        }
        if (viewPhotoModeToggleAction.action.triggered && !isInCameraMode && shotImages.Count > 0)
        {
            isInViewPhotoMode = !isInViewPhotoMode;
            photoGalleryPanel.SetActive(isInViewPhotoMode);
            if (isInViewPhotoMode)
            {
                DisplayFotosFromIndex(currentPhotoStartIndex);
            }
        }

        if (isInCameraMode)
        {
            if (scrollWheelAction != null)
            {
                float scrollValue = scrollWheelAction.action.ReadValue<float>();
                if (scrollValue > 0f)
                {
                    mainCamera.fieldOfView = Mathf.Max(maxZoomInFOV, mainCamera.fieldOfView - 5f);
                }
                else if (scrollValue < 0f)
                {
                    mainCamera.fieldOfView = Mathf.Min(maxZoomOutFOV, mainCamera.fieldOfView + 5f);
                }
            }

            ShootPicture();
        }
        else if (isInViewPhotoMode)
        {
            if (scrollWheelAction != null && scrollWheelAction.action.triggered)
            {
                float scrollValue = scrollWheelAction.action.ReadValue<float>();
                if (scrollValue != 0f)
                {
                    int maxStartIndex = Mathf.Max(0, ((shotImages.Count - 1) / photoDisplays.Count) * photoDisplays.Count);

                    if (scrollValue < 0f)
                    {
                        currentPhotoStartIndex = Mathf.Clamp(currentPhotoStartIndex + photoDisplays.Count, 0, maxStartIndex);
                    }
                    else if (scrollValue > 0f)
                    {
                        currentPhotoStartIndex = Mathf.Clamp(currentPhotoStartIndex - photoDisplays.Count, 0, maxStartIndex);
                    }

                    DisplayFotosFromIndex(currentPhotoStartIndex);
                }
            }
        }
        else
        {
            mainCamera.fieldOfView = normalFOV;
        }
    }

    private void DisplayFotosFromIndex(int startIndex)
    {
        for (int i = 0; i < photoDisplays.Count; i++)
        {
            int photoIndex = startIndex + i;
            if (photoIndex < shotImages.Count)
            {
                photoDisplays[i].sprite = shotImages[photoIndex];
                photoDisplays[i].gameObject.SetActive(true);
            }
            else
            {
                photoDisplays[i].gameObject.SetActive(false);
            }
        }

        if (pageCountText != null)
        {
            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)shotImages.Count / photoDisplays.Count));
            int currentPage = (startIndex / photoDisplays.Count) + 1;
            pageCountText.text = $"{currentPage} / {totalPages}";
        }
    }


    void ShootPicture()
    {
        if (lefMouseClickAction.action.triggered)
        {
            StartCoroutine(FlashRecordingIndicator());

            mainCamera.targetTexture = renderTexture;

            Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
            mainCamera.Render();

            RenderTexture.active = renderTexture;
            screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
            screenShot.Apply();

            mainCamera.targetTexture = null;
            RenderTexture.active = null;

            // byte[] bytes = screenShot.EncodeToPNG();
            // string filename = string.Format("{0}/Screenshots/screenshot_{1}.png", Application.persistentDataPath, System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            // System.IO.File.WriteAllBytes(filename, bytes);
            shotImages.Add(Sprite.Create(screenShot, new Rect(0, 0, screenShot.width, screenShot.height), new Vector2(0.5f, 0.5f)));
            // lastShotImage.sprite = Sprite.Create(screenShot, new Rect(0, 0, screenShot.width, screenShot.height), new Vector2(0.5f, 0.5f));
        }
    }

    private IEnumerator FlashRecordingIndicator()
    {
        isRecordingIndicator.SetActive(true);
        yield return new WaitForSeconds(0.2f);
        isRecordingIndicator.SetActive(false);
    }

    private void OnEnable()
    {
        if (cameraModeToggleAction != null)
            cameraModeToggleAction.action.Enable();
        if (lefMouseClickAction != null)
            lefMouseClickAction.action.Enable();
        if (scrollWheelAction != null)
            scrollWheelAction.action.Enable();
    }
    private void OnDisable()
    {
        if (cameraModeToggleAction != null)
            cameraModeToggleAction.action.Disable();
        if (lefMouseClickAction != null)
            lefMouseClickAction.action.Disable();
        if (scrollWheelAction != null)
            scrollWheelAction.action.Disable();
    }
}
