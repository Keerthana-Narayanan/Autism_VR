using UnityEngine;
using UnityEngine.UI;

public class TypingDots : MonoBehaviour
{
    public Image dot1, dot2, dot3;
    float t = 0f;

    void Update()
    {
        t += Time.deltaTime * 3f;
        dot1.color = new Color(0.49f, 0.23f, 0.93f,
                     Mathf.Abs(Mathf.Sin(t)));
        dot2.color = new Color(0.49f, 0.23f, 0.93f,
                     Mathf.Abs(Mathf.Sin(t + 0.6f)));
        dot3.color = new Color(0.49f, 0.23f, 0.93f,
                     Mathf.Abs(Mathf.Sin(t + 1.2f)));
    }
}
