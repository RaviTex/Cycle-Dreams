using UnityEngine;
using UnityEngine.SceneManagement;
public class PlaySceneLoader : MonoBehaviour
{
    public void LoadNormalScene()
    {
        SceneManager.LoadScene("PlaytestNormal");
    }
    public void LoadSplineScene()
    {
        SceneManager.LoadScene("PlaytestSpline");
    }
}
