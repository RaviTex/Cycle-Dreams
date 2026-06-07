using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private GameObject prototypeCompleteUI;
    [SerializeField] private GameObject explainNormalInputsUI;
    [SerializeField] private GameObject explainSplineInputsUI;
    [SerializeField] private GameObject explainPhotoModeUI;
    [SerializeField] private GameObject explainViewModeUI;
    [SerializeField] private GameObject explainOffRoadUI;
    [SerializeField] private GameObject explainGameUI;
    [SerializeField] private GameObject gameOverUI;
    [SerializeField] private GameObject reminderUI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void PrototypeComplete()
    {
        prototypeCompleteUI.SetActive(true);
    }

    public void ShowExplainInputs()
    {
        explainNormalInputsUI.SetActive(true);
    }
    public void HideExplainInputs()
    {
        explainNormalInputsUI.SetActive(false);
    }
    public void ShowExplainSplineInputs()
    {
        explainSplineInputsUI.SetActive(true);
    }
    public void HideExplainSplineInputs()
    {
        explainSplineInputsUI.SetActive(false);
    }
    public void ShowExplainPhotoMode()
    {
        explainPhotoModeUI.SetActive(true);
    }
    public void HideExplainPhotoMode()
    {
        explainPhotoModeUI.SetActive(false);
    }
    public void ShowExplainViewMode()
    {
        explainViewModeUI.SetActive(true);
    }
    public void HideExplainViewMode()
    {
        explainViewModeUI.SetActive(false);
    }
    public void ShowExplainOffRoad()
    {
        explainOffRoadUI.SetActive(true);
    }
    public void HideExplainOffRoad()
    {
        explainOffRoadUI.SetActive(false);
    }
    public void ShowGameOver()
    {
        gameOverUI.SetActive(true);
    }
    public void ShowExplainGame()
    {
        explainGameUI.SetActive(true);
    }
    public void HideExplainGame()
    {
        explainGameUI.SetActive(false);
    }
    public void ShowReminder()
    {
        reminderUI.SetActive(true);
    }
    public void HideReminder()
    {
        reminderUI.SetActive(false);
    }
}