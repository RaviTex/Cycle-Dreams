using UnityEngine;
using UnityEngine.SceneManagement;

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

    private string prototypeCompleteUIPath;
    private string explainNormalInputsUIPath;
    private string explainSplineInputsUIPath;
    private string explainPhotoModeUIPath;
    private string explainViewModeUIPath;
    private string explainOffRoadUIPath;
    private string explainGameUIPath;
    private string gameOverUIPath;
    private string reminderUIPath;

    private bool _firstSceneLoaded = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CachePaths();
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
        if (_firstSceneLoaded)
        {
            _firstSceneLoaded = false;
            return;
        }
        prototypeCompleteUI = null;
        explainNormalInputsUI = null;
        explainSplineInputsUI = null;
        explainPhotoModeUI = null;
        explainViewModeUI = null;
        explainOffRoadUI = null;
        explainGameUI = null;
        gameOverUI = null;
        reminderUI = null;
    }

    private void CachePaths()
    {
        prototypeCompleteUIPath = GetPathOrNull(prototypeCompleteUI);
        explainNormalInputsUIPath = GetPathOrNull(explainNormalInputsUI);
        explainSplineInputsUIPath = GetPathOrNull(explainSplineInputsUI);
        explainPhotoModeUIPath = GetPathOrNull(explainPhotoModeUI);
        explainViewModeUIPath = GetPathOrNull(explainViewModeUI);
        explainOffRoadUIPath = GetPathOrNull(explainOffRoadUI);
        explainGameUIPath = GetPathOrNull(explainGameUI);
        gameOverUIPath = GetPathOrNull(gameOverUI);
        reminderUIPath = GetPathOrNull(reminderUI);
    }

    private static string GetPathOrNull(GameObject go)
    {
        if (go == null) return null;
        var path = go.name;
        var t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }

    private static GameObject FindByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var parts = path.Split('/');
        var root = GameObject.Find(parts[0]);
        if (root == null) return null;
        var t = root.transform;
        for (int i = 1; i < parts.Length && t != null; i++)
            t = t.Find(parts[i]);
        return t != null ? t.gameObject : null;
    }

    private static GameObject GetOrFind(ref GameObject field, string path)
    {
        if (field == null && !string.IsNullOrEmpty(path))
            field = FindByPath(path);
        return field;
    }

    public void PrototypeComplete()
    {
        var ui = GetOrFind(ref prototypeCompleteUI, prototypeCompleteUIPath);
        if (ui != null) ui.SetActive(true);
    }

    public void ShowExplainInputs()
    {
        var ui = GetOrFind(ref explainNormalInputsUI, explainNormalInputsUIPath);
        if (ui != null) ui.SetActive(true);
    }
    public void HideExplainInputs()
    {
        var ui = GetOrFind(ref explainNormalInputsUI, explainNormalInputsUIPath);
        if (ui != null) ui.SetActive(false);
    }
    public void ShowExplainSplineInputs()
    {
        var ui = GetOrFind(ref explainSplineInputsUI, explainSplineInputsUIPath);
        if (ui != null) ui.SetActive(true);
    }
    public void HideExplainSplineInputs()
    {
        var ui = GetOrFind(ref explainSplineInputsUI, explainSplineInputsUIPath);
        if (ui != null) ui.SetActive(false);
    }
    public void ShowExplainPhotoMode()
    {
        var ui = GetOrFind(ref explainPhotoModeUI, explainPhotoModeUIPath);
        if (ui != null) ui.SetActive(true);
    }
    public void HideExplainPhotoMode()
    {
        var ui = GetOrFind(ref explainPhotoModeUI, explainPhotoModeUIPath);
        if (ui != null) ui.SetActive(false);
    }
    public void ShowExplainViewMode()
    {
        var ui = GetOrFind(ref explainViewModeUI, explainViewModeUIPath);
        if (ui != null) ui.SetActive(true);
    }
    public void HideExplainViewMode()
    {
        var ui = GetOrFind(ref explainViewModeUI, explainViewModeUIPath);
        if (ui != null) ui.SetActive(false);
    }
    public void ShowExplainOffRoad()
    {
        var ui = GetOrFind(ref explainOffRoadUI, explainOffRoadUIPath);
        if (ui != null) ui.SetActive(true);
    }
    public void HideExplainOffRoad()
    {
        var ui = GetOrFind(ref explainOffRoadUI, explainOffRoadUIPath);
        if (ui != null) ui.SetActive(false);
    }
    public void ShowGameOver()
    {
        var ui = GetOrFind(ref gameOverUI, gameOverUIPath);
        if (ui != null) ui.SetActive(true);
    }
    public void ShowExplainGame()
    {
        var ui = GetOrFind(ref explainGameUI, explainGameUIPath);
        if (ui != null) ui.SetActive(true);
    }
    public void HideExplainGame()
    {
        var ui = GetOrFind(ref explainGameUI, explainGameUIPath);
        if (ui != null) ui.SetActive(false);
    }
    public void ShowReminder()
    {
        var ui = GetOrFind(ref reminderUI, reminderUIPath);
        if (ui != null) ui.SetActive(true);
    }
    public void HideReminder()
    {
        var ui = GetOrFind(ref reminderUI, reminderUIPath);
        if (ui != null) ui.SetActive(false);
    }
}
