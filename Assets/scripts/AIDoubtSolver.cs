


// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using System.Collections;
// using System.Text;
// using UnityEngine.Networking;
// using System;

// public class AIDoubtSolver : MonoBehaviour
// {
//     [Header("Gemini API")]
//     public string apiKey = "AIzaSyCJzX40PpsOlLt3MwnOEbSJ5adepTolhYk";
//     private string apiUrl =
//     "https://generativelanguage.googleapis.com/v1beta/models/" +
//     "gemini-2.5-flash-lite:generateContent";

//     [Header("Screens")]
//     public GameObject tutorialScreen;
//     public GameObject chatbotScreen;

//     [Header("Main Screen")]
//     public Button askAIButton;

//     [Header("Chatbot UI")]
//     public TextMeshProUGUI chatHistoryText;
//     public TMP_InputField  questionInputField;
//     public TextMeshProUGUI statusText;
//     public Button          sendButton;
//     public Button          voiceButton;
//     public Button          quitButton;
//     public Button          speakButton;

//     [Header("Audio")]
//     public AudioSource audioSource;

//     [Header("Teaching Manager")]
//     public TeachingAudioManager teachingManager;

//     private string    lastAnswer       = "";
//     private bool      isRecording      = false;
//     private AudioClip recordedClip     = null;
//     private string    chatHistory      = "";
//     private bool      isRequestRunning = false;
//     private float     lastRequestTime  = -999f;  // allow first request immediately
//     private float     requestCooldown  = 30f;    // backs off on 429

//     private Coroutine countdownCoroutine = null;

//     const int SAMPLE_RATE     = 16000;
//     const int MAX_RECORD_SECS = 10;

//     // ===================================================
//     //  START
//     // ===================================================
//     void Start()
//     {
//         ShowTutorial();

//         if (askAIButton        != null) askAIButton.onClick.AddListener(OpenChatbot);
//         if (sendButton         != null) sendButton.onClick.AddListener(OnSendClicked);
//         if (voiceButton        != null) voiceButton.onClick.AddListener(ToggleVoice);
//         if (quitButton         != null) quitButton.onClick.AddListener(CloseChatbot);
//         if (speakButton        != null) speakButton.onClick.AddListener(SpeakLastAnswer);
//         if (questionInputField != null)
//             questionInputField.onSubmit.AddListener((_) => OnSendClicked());
//     }

//     // ===================================================
//     //  SHOW TUTORIAL
//     // ===================================================
//     void ShowTutorial()
//     {
//         if (tutorialScreen != null) tutorialScreen.SetActive(true);
//         if (chatbotScreen  != null) chatbotScreen.SetActive(false);
//     }

//     // ===================================================
//     //  OPEN CHATBOT
//     // ===================================================
//     public void OpenChatbot()
//     {
//         if (tutorialScreen != null) tutorialScreen.SetActive(false);
//         if (chatbotScreen  != null) chatbotScreen.SetActive(true);

//         if (teachingManager != null) teachingManager.PauseLesson();

//         chatHistory = "";
//         UpdateChatDisplay(
//             "<color=#00E5FF><b>AI Teacher</b></color>\n\n" +
//             "Hi! I am your AI teacher.\n\n" +
//             "You can:\n" +
//             "* Type your question and press SEND\n" +
//             "* Press VOICE to speak your question\n\n" +
//             "Ask me anything about Logistic Regression!");

//         SetStatus("[ READY ]");

//         if (questionInputField != null)
//         {
//             questionInputField.text = "";
//             questionInputField.Select();
//             questionInputField.ActivateInputField();
//         }
//     }

//     public void OpenPanel()  => OpenChatbot();
//     public void OpenDrawer() => OpenChatbot();

//     // ===================================================
//     //  CLOSE CHATBOT
//     // ===================================================
//     public void CloseChatbot()
//     {
//         if (isRecording)
//         {
//             Microphone.End(null);
//             isRecording = false;
//         }

//         if (audioSource != null && audioSource.isPlaying)
//             audioSource.Stop();

//         if (countdownCoroutine != null)
//         {
//             StopCoroutine(countdownCoroutine);
//             countdownCoroutine = null;
//         }

//         if (chatbotScreen  != null) chatbotScreen.SetActive(false);
//         if (tutorialScreen != null) tutorialScreen.SetActive(true);

//         if (teachingManager != null) teachingManager.ResumeLesson();
//     }

//     // ===================================================
//     //  SEND TEXT
//     // ===================================================
//     void OnSendClicked()
//     {
//         float elapsed   = Time.time - lastRequestTime;
//         float remaining = requestCooldown - elapsed;

//         if (remaining > 0f)
//         {
//             if (countdownCoroutine == null)
//                 countdownCoroutine = StartCoroutine(ShowCountdown(remaining));
//             return;
//         }

//         if (isRequestRunning)
//         {
//             SetStatus("Please wait for the AI response...");
//             return;
//         }

//         if (questionInputField == null) return;

//         string question = questionInputField.text.Trim();
//         if (question == "")
//         {
//             SetStatus("Please type a question!");
//             return;
//         }

//         AddToChat("<color=#00E5FF>You:</color> " + question);
//         questionInputField.text = "";

//         isRequestRunning = true;
//         lastRequestTime  = Time.time;
//         SetSendInteractable(false);

//         StartCoroutine(AskGeminiText(question));
//     }

//     // ===================================================
//     //  COUNTDOWN  — plain ASCII only, no emoji
//     // ===================================================
//     IEnumerator ShowCountdown(float seconds)
//     {
//         SetSendInteractable(false);

//         float end = Time.time + seconds;
//         while (Time.time < end)
//         {
//             int secs = Mathf.CeilToInt(end - Time.time);
//             SetStatus("Wait " + secs + "s before next question...");
//             yield return new WaitForSeconds(0.5f);
//         }

//         SetStatus("[ READY ]");
//         SetSendInteractable(true);
//         countdownCoroutine = null;
//     }

//     // ===================================================
//     //  VOICE TOGGLE
//     // ===================================================
//     void ToggleVoice()
//     {
//         if (!isRecording) StartRecording();
//         else              StopRecordingAndAsk();
//     }

//     void StartRecording()
//     {
//         if (Microphone.devices.Length == 0)
//         {
//             SetStatus("No microphone found!");
//             return;
//         }

//         isRecording  = true;
//         recordedClip = Microphone.Start(null, false, MAX_RECORD_SECS, SAMPLE_RATE);

//         SetStatus("Listening... click STOP when done");
//         SetBtnText(voiceButton, "STOP");
//         AddToChat("<color=#6C2FFF>Recording...</color>");
//         StartCoroutine(AutoStop());
//     }

//     IEnumerator AutoStop()
//     {
//         yield return new WaitForSeconds(MAX_RECORD_SECS);
//         if (isRecording) StopRecordingAndAsk();
//     }

//     void StopRecordingAndAsk()
//     {
//         if (!isRecording) return;
//         isRecording = false;

//         int lastSample = Microphone.GetPosition(null);
//         Microphone.End(null);

//         SetBtnText(voiceButton, "VOICE");
//         SetStatus("Processing your voice...");

//         if (recordedClip == null || lastSample <= 0)
//         {
//             SetStatus("No audio! Try again.");
//             return;
//         }

//         float[] samples = new float[lastSample * recordedClip.channels];
//         recordedClip.GetData(samples, 0);

//         byte[] wavBytes = ConvertToWav(samples, recordedClip.channels, SAMPLE_RATE);

//         AddToChat("<color=#6C2FFF>You (voice):</color> [Sending to AI...]");
//         StartCoroutine(AskGeminiAudio(wavBytes));
//     }

//     // ===================================================
//     //  GEMINI TEXT
//     // ===================================================
//     IEnumerator AskGeminiText(string question)
//     {
//         SetStatus("Thinking...");

//         string prompt =
//             "You are a helpful AI teacher explaining Logistic Regression. " +
//             "The student is using an interactive Unity tutorial with live " +
//             "sigmoid curve and weight/bias sliders. " +
//             "Answer clearly and friendly in max 80 words. " +
//             "No markdown symbols. " +
//             "Student question: " + question;

//         string body =
//             "{\"contents\":[{\"parts\":" +
//             "[{\"text\":\"" + EscapeJson(prompt) + "\"}]}]}";

//         yield return StartCoroutine(
//             SendToGemini(Encoding.UTF8.GetBytes(body)));
//     }

//     // ===================================================
//     //  GEMINI AUDIO
//     // ===================================================
//     IEnumerator AskGeminiAudio(byte[] wav)
//     {
//         SetStatus("Sending voice to AI...");

//         string base64 = Convert.ToBase64String(wav);

//         string prompt =
//             "You are a helpful AI teacher explaining Logistic Regression. " +
//             "The student asked a question via voice. " +
//             "Listen and answer clearly in max 80 words. No markdown symbols.";

//         string body =
//             "{\"contents\":[{\"parts\":[" +
//             "{\"inline_data\":{" +
//             "\"mime_type\":\"audio/wav\"," +
//             "\"data\":\"" + base64 + "\"}}," +
//             "{\"text\":\"" + EscapeJson(prompt) + "\"}]}]}";

//         yield return StartCoroutine(
//             SendToGemini(Encoding.UTF8.GetBytes(body)));
//     }

//     // ===================================================
//     //  SEND TO GEMINI  (exponential back-off on 429)
//     // ===================================================
//     IEnumerator SendToGemini(byte[] body)
//     {
//         string url = apiUrl + "?key=" + apiKey;

//         using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
//         {
//             req.uploadHandler   = new UploadHandlerRaw(body);
//             req.downloadHandler = new DownloadHandlerBuffer();
//             req.SetRequestHeader("Content-Type", "application/json");

//             yield return req.SendWebRequest();

//             // ---------- 429 Rate Limit ----------
//             if (req.responseCode == 429)
//             {
//                 requestCooldown = Mathf.Min(requestCooldown * 2f, 120f);
//                 requestCooldown = 30f;

//                 lastRequestTime = Time.time;

//                 int waitSecs = Mathf.RoundToInt(requestCooldown);

//                 AddToChat(
//                     "<color=#FF4444>AI: Too many requests! " +
//                     "Please wait " + waitSecs + " seconds and try again.</color>");

//                 isRequestRunning = false;

//                 if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
//                 countdownCoroutine = StartCoroutine(ShowCountdown(requestCooldown));

//                 yield break;
//             }

//             // ---------- Success ----------
//             if (req.result == UnityWebRequest.Result.Success)
//             {
//                 string answer = ParseResponse(req.downloadHandler.text);
//                 lastAnswer = answer;

//                 AddToChat("<color=#00FF88>AI Teacher:</color> " + answer);

//                 requestCooldown  = 30f;   // reset back-off after success
//                 isRequestRunning = false;
//                 SetStatus("[ READY ]");
//                 SetSendInteractable(true);

//                 StartCoroutine(Speak(answer));
//             }
//             // ---------- Network error ----------
//             else
//             {
//                 Debug.LogError(req.error);
//                 Debug.LogError(req.downloadHandler.text);

//                 AddToChat(
//                     "<color=#FF4444>AI: Connection failed. " +
//                     "Check your internet and try again!</color>");

//                 SetStatus("Error! Try again.");
//                 isRequestRunning = false;
//                 SetSendInteractable(true);
//             }
//         }
//     }

//     // ===================================================
//     //  PARSE RESPONSE
//     // ===================================================
//     string ParseResponse(string json)
//     {
//         try
//         {
//             int i = json.IndexOf("\"text\":");
//             if (i == -1) return "No response received.";

//             int s = json.IndexOf("\"", i + 7) + 1;
//             int e = json.IndexOf("\"", s);
//             if (e <= s) return "Empty response.";

//             return json.Substring(s, e - s)
//                 .Replace("\\n",  "\n")
//                 .Replace("\\\"", "\"")
//                 .Replace("\\\\", "\\");
//         }
//         catch (Exception ex)
//         {
//             Debug.LogError(ex.Message);
//             return "Could not read response.";
//         }
//     }

//     // ===================================================
//     //  CHAT DISPLAY
//     // ===================================================
//     void AddToChat(string message)
//     {
//         chatHistory += message + "\n\n";
//         UpdateChatDisplay(chatHistory);
//     }

//     void UpdateChatDisplay(string text)
//     {
//         if (chatHistoryText != null)
//             chatHistoryText.text = text;
//     }

//     // ===================================================
//     //  TEXT TO SPEECH
//     // ===================================================
//     void SpeakLastAnswer()
//     {
//         if (string.IsNullOrEmpty(lastAnswer)) return;
//         StartCoroutine(Speak(lastAnswer));
//     }

//     IEnumerator Speak(string text)
//     {
//         SetStatus("Speaking...");

//         string url =
//             "https://translate.google.com/translate_tts?ie=UTF-8&q=" +
//             UnityWebRequest.EscapeURL(text) +
//             "&tl=en&client=tw-ob";

//         using (UnityWebRequest req =
//             UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
//         {
//             yield return req.SendWebRequest();

//             if (req.result == UnityWebRequest.Result.Success)
//             {
//                 if (audioSource != null)
//                 {
//                     audioSource.clip = DownloadHandlerAudioClip.GetContent(req);
//                     audioSource.Play();
//                     yield return new WaitUntil(() => !audioSource.isPlaying);
//                 }
//             }
//         }

//         SetStatus("[ READY ]");
//     }

//     // ===================================================
//     //  CONVERT TO WAV
//     // ===================================================
//     byte[] ConvertToWav(float[] samples, int channels, int sampleRate)
//     {
//         int byteCount = samples.Length * 2;
//         int totalSize = 44 + byteCount;
//         byte[] wav    = new byte[totalSize];

//         Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
//         BitConverter.GetBytes(totalSize - 8).CopyTo(wav, 4);
//         Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);
//         Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
//         BitConverter.GetBytes(16).CopyTo(wav, 16);
//         BitConverter.GetBytes((short)1).CopyTo(wav, 20);
//         BitConverter.GetBytes((short)channels).CopyTo(wav, 22);
//         BitConverter.GetBytes(sampleRate).CopyTo(wav, 24);
//         BitConverter.GetBytes(sampleRate * channels * 2).CopyTo(wav, 28);
//         BitConverter.GetBytes((short)(channels * 2)).CopyTo(wav, 32);
//         BitConverter.GetBytes((short)16).CopyTo(wav, 34);
//         Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
//         BitConverter.GetBytes(byteCount).CopyTo(wav, 40);

//         int offset = 44;
//         foreach (float s in samples)
//         {
//             short val = (short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue);
//             BitConverter.GetBytes(val).CopyTo(wav, offset);
//             offset += 2;
//         }

//         return wav;
//     }

//     // ===================================================
//     //  HELPERS
//     // ===================================================
//     void SetStatus(string msg)
//     {
//         if (statusText != null) statusText.text = msg;
//     }

//     void SetSendInteractable(bool value)
//     {
//         if (sendButton != null) sendButton.interactable = value;
//     }

//     void SetBtnText(Button btn, string text)
//     {
//         if (btn == null) return;
//         var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
//         if (tmp != null) tmp.text = text;
//     }

//     static string EscapeJson(string s)
//     {
//         return s
//             .Replace("\\", "\\\\")
//             .Replace("\"", "\\\"")
//             .Replace("\n", "\\n")
//             .Replace("\r", "\\r")
//             .Replace("\t", "\\t");
//     }
// }


using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Text;
using UnityEngine.Networking;
using System;

// ══════════════════════════════════════════════════════════════════
//  AIDoubtSolver  —  Modern Bubble Chat UI
//
//  HIERARCHY REQUIRED:
//
//  Canvas
//  ├── TutorialScreen          (your existing tutorial)
//  └── ChatbotScreen
//      ├── Background          (Image, color #0A0A0F)
//      ├── HeaderBar
//      │   ├── AvatarCircle    (Image, circle, purple gradient)
//      │   ├── HeaderTitle     (TMP "AI Teacher")
//      │   └── CloseBtn        (Button "✕")
//      ├── ScrollView          (Scroll Rect, vertical only)
//      │   └── Viewport
//      │       └── ContentArea (Vertical Layout Group + Content Size Fitter)
//      ├── TypingIndicator     (TMP "AI is thinking...")  ← hidden by default
//      └── InputBar
//          ├── InputBackground (Image, rounded, #1A1A2E)
//          ├── InputField      (TMP_InputField)
//          ├── VoiceBtn        (Button — mic icon)
//          └── SendBtn         (Button — send icon)
//
//  MESSAGE BUBBLE PREFABS (create two prefabs):
//
//  UserBubble  (HorizontalLayoutGroup, right-aligned)
//  └── BubbleBackground  (Image, rounded, #7C3AED)
//      └── MessageText   (TMP)
//
//  BotBubble   (HorizontalLayoutGroup, left-aligned)
//  ├── AvatarDot   (Image, small circle, #7C3AED)
//  └── BubbleBackground  (Image, rounded, #1A1A2E)
//      └── MessageText   (TMP)
//
// ══════════════════════════════════════════════════════════════════

public class AIDoubtSolver : MonoBehaviour
{
    // ── API ──────────────────────────────────────────────────────
    [Header("Gemini API")]
    public string apiKey = "AIzaSyCJzX40PpsOlLt3MwnOEbSJ5adepTolhYk";
    private string apiUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/" +
        "gemini-2.5-flash-lite:generateContent";

    // ── SCREENS ──────────────────────────────────────────────────
    [Header("Screens")]
    public GameObject tutorialScreen;
    public GameObject chatbotScreen;

    // ── HEADER ───────────────────────────────────────────────────
    [Header("Header")]
    public Button closeButton;          // ✕ in header bar

    // ── CHAT AREA ────────────────────────────────────────────────
    [Header("Chat Area")]
    public ScrollRect  chatScrollRect;  // the ScrollRect component
    public RectTransform contentArea;   // ContentArea inside Viewport
    public GameObject  userBubblePrefab;
    public GameObject  botBubblePrefab;
    public GameObject  typingIndicator; // "AI is thinking..." row

    // ── INPUT BAR ────────────────────────────────────────────────
    [Header("Input Bar")]
    public TMP_InputField questionInputField;
    public Button         sendButton;
    public Button         voiceButton;

    // ── LEGACY / COMPAT ──────────────────────────────────────────
    [Header("Legacy (optional — leave empty if not used)")]
    public Button          speakButton;
    public TextMeshProUGUI statusText;   // small status label (optional)

    // ── AUDIO ────────────────────────────────────────────────────
    [Header("Audio")]
    public AudioSource audioSource;

    // ── TEACHING MANAGER ─────────────────────────────────────────
    [Header("Teaching Manager")]
    public TeachingAudioManager teachingManager;

    // ── PRIVATE STATE ─────────────────────────────────────────────
    private string    lastAnswer       = "";
    private bool      isRecording      = false;
    private AudioClip recordedClip     = null;
    private bool      isRequestRunning = false;
    private float     lastRequestTime  = -999f;
    private float     requestCooldown  = 30f;
    private Coroutine countdownCoroutine = null;

    const int SAMPLE_RATE     = 16000;
    const int MAX_RECORD_SECS = 10;

    // ═════════════════════════════════════════════════════════════
    //  START
    // ═════════════════════════════════════════════════════════════
    void Start()
    {
        ShowTutorial();

        if (closeButton        != null) closeButton.onClick.AddListener(CloseChatbot);
        if (sendButton         != null) sendButton.onClick.AddListener(OnSendClicked);
        if (voiceButton        != null) voiceButton.onClick.AddListener(ToggleVoice);
        if (speakButton        != null) speakButton.onClick.AddListener(SpeakLastAnswer);
        if (typingIndicator    != null) typingIndicator.SetActive(false);

        if (questionInputField != null)
            questionInputField.onSubmit.AddListener((_) => OnSendClicked());
    }

    // ═════════════════════════════════════════════════════════════
    //  SHOW TUTORIAL
    // ═════════════════════════════════════════════════════════════
    void ShowTutorial()
    {
        if (tutorialScreen != null) tutorialScreen.SetActive(true);
        if (chatbotScreen  != null) chatbotScreen.SetActive(false);
    }

    // ═════════════════════════════════════════════════════════════
    //  OPEN CHATBOT
    // ═════════════════════════════════════════════════════════════
    public void OpenChatbot()
    {
        if (tutorialScreen != null) tutorialScreen.SetActive(false);
        if (chatbotScreen  != null) chatbotScreen.SetActive(true);

        if (teachingManager != null) teachingManager.PauseLesson();

        // Clear old bubbles
        ClearChat();

        // Welcome bubble from bot
        StartCoroutine(SpawnBotBubbleAnimated(
            "Hi! I am your AI Teacher.\n\nAsk me anything about Logistic Regression — " +
            "type your question or press the mic button to speak.", delay: 0.3f));

        SetStatus("READY");
        SetSendInteractable(true);

        if (questionInputField != null)
        {
            questionInputField.text = "";
            questionInputField.Select();
            questionInputField.ActivateInputField();
        }
    }

    public void OpenPanel()  => OpenChatbot();
    public void OpenDrawer() => OpenChatbot();

    // ═════════════════════════════════════════════════════════════
    //  CLOSE CHATBOT
    // ═════════════════════════════════════════════════════════════
    public void CloseChatbot()
    {
        if (isRecording) { Microphone.End(null); isRecording = false; }

        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();

        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        if (typingIndicator != null) typingIndicator.SetActive(false);

        if (chatbotScreen  != null) chatbotScreen.SetActive(false);
        if (tutorialScreen != null) tutorialScreen.SetActive(true);

        if (teachingManager != null) teachingManager.ResumeLesson();
    }

    // ═════════════════════════════════════════════════════════════
    //  SEND TEXT
    // ═════════════════════════════════════════════════════════════
    void OnSendClicked()
    {
        float elapsed   = Time.time - lastRequestTime;
        float remaining = requestCooldown - elapsed;

        if (remaining > 0f)
        {
            if (countdownCoroutine == null)
                countdownCoroutine = StartCoroutine(ShowCountdown(remaining));
            return;
        }

        if (isRequestRunning) return;
        if (questionInputField == null) return;

        string question = questionInputField.text.Trim();
        if (question == "") return;

        SpawnUserBubble(question);
        questionInputField.text = "";

        isRequestRunning = true;
        lastRequestTime  = Time.time;
        SetSendInteractable(false);
        ShowTyping(true);

        StartCoroutine(AskGeminiText(question));
    }

    // ═════════════════════════════════════════════════════════════
    //  COUNTDOWN
    // ═════════════════════════════════════════════════════════════
    IEnumerator ShowCountdown(float seconds)
    {
        SetSendInteractable(false);
        float end = Time.time + seconds;

        while (Time.time < end)
        {
            int secs = Mathf.CeilToInt(end - Time.time);
            SetStatus("Wait " + secs + "s...");
            yield return new WaitForSeconds(0.5f);
        }

        SetStatus("READY");
        SetSendInteractable(true);
        countdownCoroutine = null;
    }

    // ═════════════════════════════════════════════════════════════
    //  VOICE
    // ═════════════════════════════════════════════════════════════
    void ToggleVoice()
    {
        if (!isRecording) StartRecording();
        else              StopRecordingAndAsk();
    }

    void StartRecording()
    {
        if (Microphone.devices.Length == 0) { SetStatus("No mic found!"); return; }

        isRecording  = true;
        recordedClip = Microphone.Start(null, false, MAX_RECORD_SECS, SAMPLE_RATE);

        SetStatus("Listening...");
        SetVoiceBtnRecording(true);
        SpawnUserBubble("🎤 Recording... (tap mic to stop)");
        StartCoroutine(AutoStop());
    }

    IEnumerator AutoStop()
    {
        yield return new WaitForSeconds(MAX_RECORD_SECS);
        if (isRecording) StopRecordingAndAsk();
    }

    void StopRecordingAndAsk()
    {
        if (!isRecording) return;
        isRecording = false;

        int lastSample = Microphone.GetPosition(null);
        Microphone.End(null);
        SetVoiceBtnRecording(false);
        SetStatus("Processing...");

        if (recordedClip == null || lastSample <= 0)
        {
            SpawnBotBubble("I couldn't hear anything. Please try again.");
            SetStatus("READY");
            return;
        }

        float[] samples = new float[lastSample * recordedClip.channels];
        recordedClip.GetData(samples, 0);
        byte[] wavBytes = ConvertToWav(samples, recordedClip.channels, SAMPLE_RATE);

        ShowTyping(true);
        StartCoroutine(AskGeminiAudio(wavBytes));
    }

    // ═════════════════════════════════════════════════════════════
    //  GEMINI TEXT
    // ═════════════════════════════════════════════════════════════
    IEnumerator AskGeminiText(string question)
    {
        string prompt =
            "You are a helpful AI teacher explaining Logistic Regression. " +
            "The student is using an interactive Unity tutorial with live " +
            "sigmoid curve and weight/bias sliders. " +
            "Answer clearly and warmly in max 80 words. No markdown symbols. " +
            "Student question: " + question;

        string body =
            "{\"contents\":[{\"parts\":" +
            "[{\"text\":\"" + EscapeJson(prompt) + "\"}]}]}";

        yield return StartCoroutine(SendToGemini(Encoding.UTF8.GetBytes(body)));
    }

    // ═════════════════════════════════════════════════════════════
    //  GEMINI AUDIO
    // ═════════════════════════════════════════════════════════════
    IEnumerator AskGeminiAudio(byte[] wav)
    {
        string base64 = Convert.ToBase64String(wav);
        string prompt =
            "You are a helpful AI teacher for Logistic Regression. " +
            "The student asked a question via voice. " +
            "Answer clearly in max 80 words. No markdown.";

        string body =
            "{\"contents\":[{\"parts\":[" +
            "{\"inline_data\":{\"mime_type\":\"audio/wav\",\"data\":\"" + base64 + "\"}}," +
            "{\"text\":\"" + EscapeJson(prompt) + "\"}]}]}";

        yield return StartCoroutine(SendToGemini(Encoding.UTF8.GetBytes(body)));
    }

    // ═════════════════════════════════════════════════════════════
    //  SEND TO GEMINI
    // ═════════════════════════════════════════════════════════════
    IEnumerator SendToGemini(byte[] body)
    {
        string url = apiUrl + "?key=" + apiKey;

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            ShowTyping(false);

            if (req.responseCode == 429)
            {
                requestCooldown = 30f;
                lastRequestTime = Time.time;
                isRequestRunning = false;

                SpawnBotBubble("I'm a little overwhelmed right now. Please wait 30 seconds and try again!");

                if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
                countdownCoroutine = StartCoroutine(ShowCountdown(requestCooldown));
                yield break;
            }

            if (req.result == UnityWebRequest.Result.Success)
            {
                string answer = ParseResponse(req.downloadHandler.text);
                lastAnswer = answer;

                StartCoroutine(SpawnBotBubbleAnimated(answer, delay: 0.1f));

                requestCooldown  = 30f;
                isRequestRunning = false;
                SetStatus("READY");
                SetSendInteractable(true);

                StartCoroutine(Speak(answer));
            }
            else
            {
                Debug.LogError("[Gemini] " + req.error);
                SpawnBotBubble("Connection failed. Please check your internet and try again.");
                SetStatus("Error");
                isRequestRunning = false;
                SetSendInteractable(true);
            }
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  BUBBLE SPAWNING
    // ═════════════════════════════════════════════════════════════

    /// <summary>Spawn a USER bubble immediately.</summary>
    void SpawnUserBubble(string text)
    {
        if (userBubblePrefab == null || contentArea == null) return;

        GameObject bubble = Instantiate(userBubblePrefab, contentArea);
        SetBubbleText(bubble, text);
        SetBubbleAlpha(bubble, 0f);
        StartCoroutine(FadeInBubble(bubble, 0f));
        StartCoroutine(ScrollToBottom());
    }

    /// <summary>Spawn a BOT bubble immediately.</summary>
    void SpawnBotBubble(string text)
    {
        if (botBubblePrefab == null || contentArea == null) return;

        GameObject bubble = Instantiate(botBubblePrefab, contentArea);
        SetBubbleText(bubble, text);
        SetBubbleAlpha(bubble, 0f);
        StartCoroutine(FadeInBubble(bubble, 0f));
        StartCoroutine(ScrollToBottom());
    }

    /// <summary>Spawn a BOT bubble after a short delay (simulates typing).</summary>
    IEnumerator SpawnBotBubbleAnimated(string text, float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnBotBubble(text);
    }

    void SetBubbleText(GameObject bubble, string text)
    {
        var tmp = bubble.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
    }

    void SetBubbleAlpha(GameObject bubble, float alpha)
    {
        var cg = bubble.GetComponent<CanvasGroup>();
        if (cg == null) cg = bubble.AddComponent<CanvasGroup>();
        cg.alpha = alpha;
    }

    IEnumerator FadeInBubble(GameObject bubble, float startDelay)
    {
        yield return new WaitForSeconds(startDelay);

        var cg = bubble.GetComponent<CanvasGroup>();
        if (cg == null) cg = bubble.AddComponent<CanvasGroup>();

        // Also slide in from slight offset
        RectTransform rt = bubble.GetComponent<RectTransform>();
        Vector2 startPos = rt != null ? rt.anchoredPosition + new Vector2(0, -12f) : Vector2.zero;
        Vector2 endPos   = rt != null ? rt.anchoredPosition : Vector2.zero;

        float duration = 0.25f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            cg.alpha = t;
            if (rt != null) rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        cg.alpha = 1f;
        StartCoroutine(ScrollToBottom());
    }

    void ClearChat()
    {
        if (contentArea == null) return;
        foreach (Transform child in contentArea)
            Destroy(child.gameObject);
    }

    // ═════════════════════════════════════════════════════════════
    //  TYPING INDICATOR
    // ═════════════════════════════════════════════════════════════
    void ShowTyping(bool show)
    {
        if (typingIndicator != null)
            typingIndicator.SetActive(show);

        if (show) StartCoroutine(ScrollToBottom());
    }

    // ═════════════════════════════════════════════════════════════
    //  AUTO SCROLL
    // ═════════════════════════════════════════════════════════════
    IEnumerator ScrollToBottom()
    {
        // Wait one frame for layout to update
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        if (chatScrollRect != null)
        {
            // Smooth scroll
            StartCoroutine(SmoothScrollToBottom());
        }
    }

    IEnumerator SmoothScrollToBottom()
    {
        float duration = 0.3f;
        float elapsed  = 0f;
        float startVal = chatScrollRect.verticalNormalizedPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            chatScrollRect.verticalNormalizedPosition = Mathf.Lerp(startVal, 0f, t);
            yield return null;
        }

        chatScrollRect.verticalNormalizedPosition = 0f;
    }

    // ═════════════════════════════════════════════════════════════
    //  PARSE RESPONSE
    // ═════════════════════════════════════════════════════════════
    string ParseResponse(string json)
    {
        try
        {
            int i = json.IndexOf("\"text\":");
            if (i == -1) return "No response received.";
            int s = json.IndexOf("\"", i + 7) + 1;
            int e = json.IndexOf("\"", s);
            if (e <= s) return "Empty response.";
            return json.Substring(s, e - s)
                .Replace("\\n",  "\n")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.Message);
            return "Could not read response.";
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  TEXT TO SPEECH
    // ═════════════════════════════════════════════════════════════
    void SpeakLastAnswer()
    {
        if (!string.IsNullOrEmpty(lastAnswer))
            StartCoroutine(Speak(lastAnswer));
    }

    IEnumerator Speak(string text)
    {
        SetStatus("Speaking...");
        string url =
            "https://translate.google.com/translate_tts?ie=UTF-8&q=" +
            UnityWebRequest.EscapeURL(text) + "&tl=en&client=tw-ob";

        using (UnityWebRequest req =
            UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success && audioSource != null)
            {
                audioSource.clip = DownloadHandlerAudioClip.GetContent(req);
                audioSource.Play();
                yield return new WaitUntil(() => !audioSource.isPlaying);
            }
        }
        SetStatus("READY");
    }

    // ═════════════════════════════════════════════════════════════
    //  WAV CONVERSION
    // ═════════════════════════════════════════════════════════════
    byte[] ConvertToWav(float[] samples, int channels, int sampleRate)
    {
        int byteCount = samples.Length * 2;
        int totalSize = 44 + byteCount;
        byte[] wav    = new byte[totalSize];

        Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        BitConverter.GetBytes(totalSize - 8).CopyTo(wav, 4);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);
        Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
        BitConverter.GetBytes(16).CopyTo(wav, 16);
        BitConverter.GetBytes((short)1).CopyTo(wav, 20);
        BitConverter.GetBytes((short)channels).CopyTo(wav, 22);
        BitConverter.GetBytes(sampleRate).CopyTo(wav, 24);
        BitConverter.GetBytes(sampleRate * channels * 2).CopyTo(wav, 28);
        BitConverter.GetBytes((short)(channels * 2)).CopyTo(wav, 32);
        BitConverter.GetBytes((short)16).CopyTo(wav, 34);
        Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
        BitConverter.GetBytes(byteCount).CopyTo(wav, 40);

        int offset = 44;
        foreach (float s in samples)
        {
            short val = (short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue);
            BitConverter.GetBytes(val).CopyTo(wav, offset);
            offset += 2;
        }
        return wav;
    }

    // ═════════════════════════════════════════════════════════════
    //  HELPERS
    // ═════════════════════════════════════════════════════════════
    void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    void SetSendInteractable(bool value)
    {
        if (sendButton != null) sendButton.interactable = value;
    }

    void SetVoiceBtnRecording(bool recording)
    {
        if (voiceButton == null) return;
        // Tint the button red while recording
        var img = voiceButton.GetComponent<Image>();
        if (img != null)
            img.color = recording
                ? new Color(0.9f, 0.2f, 0.2f, 1f)   // red = recording
                : new Color(0.47f, 0.23f, 0.93f, 1f); // purple = idle
    }

    static string EscapeJson(string s) =>
        s.Replace("\\", "\\\\")
         .Replace("\"", "\\\"")
         .Replace("\n",  "\\n")
         .Replace("\r",  "\\r")
         .Replace("\t",  "\\t");
}