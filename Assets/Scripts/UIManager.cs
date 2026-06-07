using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private GameObject prototypeCompleteUI;
    [SerializeField] private GameObject explainInputsUI;
    [SerializeField] private GameObject explainPhotoModeUI;
    [SerializeField] private GameObject explainViewModeUI;
    [SerializeField] private GameObject explainOffRoadUI;
    [SerializeField] private GameObject explainGameUI;
    [SerializeField] private GameObject gameOverUI;

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
        explainInputsUI.SetActive(true);
    }
    public void HideExplainInputs()
    {
        explainInputsUI.SetActive(false);
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
}