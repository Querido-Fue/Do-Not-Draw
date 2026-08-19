using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class TitleButtonListener : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private TMP_Text buttonText;
    private Color normalColor = new Color(48f/255f, 48f/255f, 48f/255f);
    private Color hoverColor = new Color(200f/255f, 0, 0);
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (buttonText != null)
            buttonText.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (buttonText != null)
            buttonText.color = normalColor;
    }

    private void Awake()
    {
        if (buttonText == null)
            buttonText = GetComponentInChildren<TMP_Text>();
        
        if (buttonText != null)
            buttonText.color = normalColor;
    }
}
