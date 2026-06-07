using UnityEngine;

public class Endpoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Endpoint"))
        {
            GameManager.Instance.PrototypeComplete();
        }
        else if (other.CompareTag("Explain Inputs"))
        {
            UIManager.Instance.ShowExplainInputs();
        }
        else if (other.CompareTag("Explain Photo Mode"))
        {
            UIManager.Instance.ShowExplainPhotoMode();
        }
        else if (other.CompareTag("Explain View Mode"))
        {
            UIManager.Instance.ShowExplainViewMode();
        }
        else if (other.CompareTag("Explain Off Road"))
        {
            UIManager.Instance.ShowExplainOffRoad();
        }
        else if (other.CompareTag("Explain Game"))
        {
            UIManager.Instance.ShowExplainGame();
        }
        else if (other.CompareTag("Game Over"))
        {
            GameManager.Instance.GameOver();
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Explain Inputs"))
        {
            UIManager.Instance.HideExplainInputs();
        }
        else if (other.CompareTag("Explain Photo Mode"))
        {
            UIManager.Instance.HideExplainPhotoMode();
        }
        else if (other.CompareTag("Explain View Mode"))
        {
            UIManager.Instance.HideExplainViewMode();
        }
        else if (other.CompareTag("Explain Off Road"))
        {
            UIManager.Instance.HideExplainOffRoad();
        }
        else if (other.CompareTag("Explain Game"))
        {
            UIManager.Instance.HideExplainGame();
        }
    }
}
