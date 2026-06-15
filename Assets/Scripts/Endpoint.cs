using UnityEngine;

public class Endpoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.IsRestarting) return;
        var ui = UIManager.Instance;
        if (ui == null) return;

        if (other.CompareTag("Endpoint"))
        {
            if (gm.HasFotographedRabbit && gm.HasFotographedBear)
                gm.PrototypeComplete();
            else
                ui.ShowReminder();
        }
        else if (other.CompareTag("Explain Inputs"))
        {
            if (!gm.isSplineMode)
                ui.ShowExplainInputs();
            else
                ui.ShowExplainSplineInputs();
        }
        else if (other.CompareTag("Explain Photo Mode"))
        {
            ui.ShowExplainPhotoMode();
        }
        else if (other.CompareTag("Explain View Mode"))
        {
            ui.ShowExplainViewMode();
        }
        else if (other.CompareTag("Explain Off Road"))
        {
            ui.ShowExplainOffRoad();
        }
        else if (other.CompareTag("Explain Game"))
        {
            ui.ShowExplainGame();
        }
        else if (other.CompareTag("Game Over"))
        {
            gm.GameOver();
        }
    }
    private void OnTriggerExit(Collider other)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.IsRestarting) return;
        var ui = UIManager.Instance;
        if (ui == null) return;

        if (other.CompareTag("Explain Inputs"))
        {
            if (!gm.isSplineMode)
                ui.HideExplainInputs();
            else
                ui.HideExplainSplineInputs();
        }
        else if (other.CompareTag("Explain Photo Mode"))
        {
            ui.HideExplainPhotoMode();
        }
        else if (other.CompareTag("Explain View Mode"))
        {
            ui.HideExplainViewMode();
        }
        else if (other.CompareTag("Explain Off Road"))
        {
            ui.HideExplainOffRoad();
        }
        else if (other.CompareTag("Explain Game"))
        {
            ui.HideExplainGame();
        }
        else if (other.CompareTag("Endpoint"))
        {
            ui.HideReminder();
        }
    }
}
