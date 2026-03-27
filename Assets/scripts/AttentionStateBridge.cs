using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Minimal bridge to connect your BILSTM/Muse2 attention prediction output
/// into the VR lesson feedback loop.
///
/// Your inference code should call:
///   - OnAttentionClass(0|1|2) OR
///   - OnAttentionStateChanged("FOCUSED"/"UNFOCUSED"/"DROWSY")
/// </summary>
public class AttentionStateBridge : MonoBehaviour
{
    public ChatbotUIBuilder chatbotUI;
    public bool  autoFindChatbot = true;

    // Keyboard test (optional). Toggle from Inspector if needed.
    public bool enableKeyboardTesting = true;
    public KeyCode keyFocused = KeyCode.Alpha1;
    public KeyCode keyUnfocused = KeyCode.Alpha2;
    public KeyCode keyDrowsy = KeyCode.Alpha3;

    void Start()
    {
        if (chatbotUI == null && autoFindChatbot)
            chatbotUI = FindFirstObjectByType<ChatbotUIBuilder>();
    }

    public void OnAttentionStateChanged(string state)
    {
        if (chatbotUI == null) return;
        chatbotUI.SetFocusState(state);
    }

    // Map your model class index here:
    // 0 = FOCUSED, 1 = UNFOCUSED, 2 = DROWSY
    public void OnAttentionClass(int classIndex)
    {
        switch (classIndex)
        {
            case 0: OnAttentionStateChanged("FOCUSED"); break;
            case 1: OnAttentionStateChanged("UNFOCUSED"); break;
            case 2: OnAttentionStateChanged("DROWSY"); break;
        }
    }

    void Update()
    {
        if (!enableKeyboardTesting) return;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return;

        var k0 = ToInputSystemKey(keyFocused);
        var k1 = ToInputSystemKey(keyUnfocused);
        var k2 = ToInputSystemKey(keyDrowsy);

        if (k0.HasValue && Keyboard.current[k0.Value].wasPressedThisFrame)
            OnAttentionClass(0);
        else if (k1.HasValue && Keyboard.current[k1.Value].wasPressedThisFrame)
            OnAttentionClass(1);
        else if (k2.HasValue && Keyboard.current[k2.Value].wasPressedThisFrame)
            OnAttentionClass(2);
#else
        // Fallback for projects that still use the legacy Input manager.
        if (Input.GetKeyDown(keyFocused)) OnAttentionClass(0);
        else if (Input.GetKeyDown(keyUnfocused)) OnAttentionClass(1);
        else if (Input.GetKeyDown(keyDrowsy)) OnAttentionClass(2);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    static Key? ToInputSystemKey(KeyCode keyCode)
    {
        // Only map the keys we use for testing.
        switch (keyCode)
        {
            // Unity InputSystem uses Digit1/2/3 for top-row number keys.
            case KeyCode.Alpha1: return Key.Digit1;
            case KeyCode.Alpha2: return Key.Digit2;
            case KeyCode.Alpha3: return Key.Digit3;
            default: return null;
        }
    }
#endif
}

