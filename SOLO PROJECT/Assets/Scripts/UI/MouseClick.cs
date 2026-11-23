using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class MouseClick : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if(eventData.button == PointerEventData.InputButton.Left && GameManager.Instance.fadeOutFinish == true)
        {
            SceneManager.LoadScene("ResultScreen");
        }
    }
}
