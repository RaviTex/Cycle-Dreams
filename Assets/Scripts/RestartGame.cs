using UnityEngine;
using UnityEngine.EventSystems;

public class RestartGame : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        GameManager.Instance.RestartGame();
    }
}