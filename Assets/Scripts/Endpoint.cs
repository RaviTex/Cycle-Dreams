using UnityEngine;

public class Endpoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Endpoint"))
        {
            if (GameManager.Instance.HasFotographedRabbit && GameManager.Instance.HasFotographedBear)
                GameManager.Instance.PrototypeComplete();
            else
                UIManager.Instance.ShowReminder();
        }
        else if (other.CompareTag("Explain Inputs"))
        {
            if (!GameManager.Instance.isSplineMode)
                UIManager.Instance.ShowExplainInputs();
            else
                UIManager.Instance.ShowExplainSplineInputs();
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
            if (!GameManager.Instance.isSplineMode)
                UIManager.Instance.HideExplainInputs();
            else
                UIManager.Instance.HideExplainSplineInputs();
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
        else if (other.CompareTag("Endpoint"))
        {
            UIManager.Instance.HideReminder();
        }
    }
}
