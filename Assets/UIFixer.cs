using UnityEngine;

public class UIFixer : MonoBehaviour
{
    public RectTransform chatbotScreen;
    public RectTransform headerBar;
    public RectTransform scrollView;
    public RectTransform inputBar;

    void Start()
    {
        Fix(chatbotScreen, 0, 0, 0, 0);
        Fix(headerBar,     0, 0, 0, -70);
        Fix(scrollView,    0, 0, 70, 80);
        Fix(inputBar,      0, 0, -72, 0);
    }

    void Fix(RectTransform rt, float left, float right, float top, float bottom)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = new Vector2(left,  bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }
}