// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using System.Collections;
// using System.Text;
// using UnityEngine.Networking;

// public class AIDoubtSolver : MonoBehaviour
// {
//     [Header("Gemini API")]
//     public string apiKey ="AIzaSyDnv-GElQyA_4qjKDdEuoArurVL7AzRw5A";
//     private string apiUrl =
// "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
//     [Header("UI Elements")]
//     public GameObject aiPanel;
//     public TMP_InputField questionInputField;
//     public TextMeshProUGUI answerText;
//     public TextMeshProUGUI statusText;
//     public Button askButton;
//     public Button closeButton;
//     public Button listenButton;
//     public Button speakAnswerButton;

//     [Header("Audio")]
//     public AudioSource audioSource;

//     [Header("Teaching Video Reference")]
//     public TeachingAudioManager teachingManager;

//     private string lastAnswer = "";
//     private bool isRecording = false;

//     void Start()
//     {
//         if (aiPanel != null)
//             aiPanel.SetActive(false);

//         if (askButton != null)
//             askButton.onClick.AddListener(
//                 OnAskButtonClicked);

//         if (closeButton != null)
//             closeButton.onClick.AddListener(ClosePanel);

//         if (listenButton != null)
//             listenButton.onClick.AddListener(
//                 StartVoiceInput);

//         if (speakAnswerButton != null)
//             speakAnswerButton.onClick.AddListener(
//                 SpeakAnswer);
//     }

//     public void OpenPanel()
//     {
//         // Show AI panel
//         if (aiPanel != null)
//             aiPanel.SetActive(true);

//         // PAUSE the teaching video
//         if (teachingManager != null)
//             teachingManager.PauseLesson();

//         if (answerText != null)
//             answerText.text =
//                 "Hi! I am your AI teacher.\n" +
//                 "Ask me anything about " +
//                 "Logistic Regression!";

//         if (statusText != null)
//             statusText.text =
//                 "Lesson paused. Ask your doubt!";
//     }

//     void ClosePanel()
//     {
//         // Hide AI panel
//         if (aiPanel != null)
//             aiPanel.SetActive(false);

//         // RESUME the teaching video
//         if (teachingManager != null)
//             teachingManager.ResumeLesson();

//         // Stop AI audio
//         if (audioSource != null &&
//             audioSource.isPlaying)
//             audioSource.Stop();
//     }

//     public void OnAskButtonClicked()
//     {
//         if (questionInputField == null) return;
//         string question =
//             questionInputField.text.Trim();

//         if (question == "")
//         {
//             if (statusText != null)
//                 statusText.text =
//                     "Please type your question first!";
//             return;
//         }

//         StartCoroutine(AskGemini(question));
//     }

//     IEnumerator AskGemini(string question)
// {
//     if (statusText != null)
//         statusText.text = "Thinking...";

//     if (answerText != null)
//         answerText.text = "Please wait...";

//     if (askButton != null)
//         askButton.interactable = false;

//     string fullPrompt =
//         "You are a helpful AI teacher explaining Logistic Regression to a student. " +
//         "Keep your answer simple and under 80 words. Student question: " + question;

//     // escape quotes
//     string safePrompt = fullPrompt.Replace("\"", "\\\"");

//     string jsonBody =
//         "{\"contents\":[{\"parts\":[{\"text\":\"" + safePrompt + "\"}]}]}";

//     string fullUrl = apiUrl + "?key=" + apiKey;

//     UnityWebRequest request = new UnityWebRequest(fullUrl, "POST");

//     byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

//     request.uploadHandler = new UploadHandlerRaw(bodyRaw);
//     request.downloadHandler = new DownloadHandlerBuffer();

//     request.SetRequestHeader("Content-Type", "application/json");
//     request.SetRequestHeader("User-Agent", "Unity");

//     yield return request.SendWebRequest();

//     if (askButton != null)
//         askButton.interactable = true;

//     if (request.result == UnityWebRequest.Result.Success)
//     {
//         string response = request.downloadHandler.text;

//         Debug.Log("Gemini Response: " + response);

//         string answer = ParseGeminiResponse(response);

//         lastAnswer = answer;

//         if (answerText != null)
//             answerText.text = answer;

//         if (statusText != null)
//             statusText.text = "Answer ready!";

//         StartCoroutine(SpeakWithTTS(answer));
//     }
//     else
//     {
//         Debug.Log("Gemini Error: " + request.error);
//         Debug.Log("Server Response: " + request.downloadHandler.text);

//         if (answerText != null)
//             answerText.text =
//                 "AI connection failed.";

//         if (statusText != null)
//             statusText.text =
//                 "Error: " + request.error;
//     }
// }

//     string ParseGeminiResponse(string json)
//     {
//         try
//         {
//             int textIndex =
//                 json.IndexOf("\"text\":");
//             if (textIndex == -1)
//                 return "Could not parse response.";

//             int start =
//                 json.IndexOf("\"", textIndex + 7) + 1;
//             int end = json.IndexOf("\"", start);

//             string text =
//                 json.Substring(start, end - start);
//             text = text.Replace("\\n", "\n");
//             text = text.Replace("\\\"", "\"");
//             text = text.Replace("\\\\", "\\");

//             return text;
//         }
//         catch
//         {
//             return "Sorry I could not understand " +
//                    "the response.";
//         }
//     }

//     void StartVoiceInput()
//     {
//         if (!isRecording)
//         {
//             isRecording = true;

//             if (statusText != null)
//                 statusText.text =
//                     "Listening... Speak now!";

//             TextMeshProUGUI btnText =
//                 listenButton
//                 .GetComponentInChildren
//                 <TextMeshProUGUI>();
//             if (btnText != null)
//                 btnText.text = "Stop";

//             audioSource.clip =
//                 Microphone.Start(
//                     null, false, 10, 44100);

//             StartCoroutine(AutoStopRecording());
//         }
//         else
//         {
//             StopVoiceInput();
//         }
//     }

//     IEnumerator AutoStopRecording()
//     {
//         yield return new WaitForSeconds(8f);
//         if (isRecording)
//             StopVoiceInput();
//     }

//     void StopVoiceInput()
//     {
//         isRecording = false;
//         Microphone.End(null);

//         TextMeshProUGUI btnText =
//             listenButton
//             .GetComponentInChildren
//             <TextMeshProUGUI>();
//         if (btnText != null)
//             btnText.text = "Ask by Voice";

//         if (statusText != null)
//             statusText.text =
//                 "Press Windows + H to type by voice!";
//     }

//     void SpeakAnswer()
//     {
//         if (lastAnswer == "") return;
//         StartCoroutine(SpeakWithTTS(lastAnswer));
//     }

//     IEnumerator SpeakWithTTS(string text)
//     {
//         if (statusText != null)
//             statusText.text = "Speaking answer...";

//         string encodedText =
//             UnityWebRequest.EscapeURL(text);

//         string ttsUrl =
//             "https://translate.google.com/translate_tts" +
//             "?ie=UTF-8&q=" + encodedText +
//             "&tl=en&client=tw-ob";

//         UnityWebRequest request =
//             UnityWebRequestMultimedia.GetAudioClip(
//                 ttsUrl, AudioType.MPEG);

//         yield return request.SendWebRequest();

//         if (request.result ==
//             UnityWebRequest.Result.Success)
//         {
//             AudioClip clip =
//                 DownloadHandlerAudioClip
//                 .GetContent(request);
//             audioSource.clip = clip;
//             audioSource.Play();

//             if (statusText != null)
//                 statusText.text =
//                     "Playing answer...";
//         }
//         else
//         {
//             if (statusText != null)
//                 statusText.text =
//                     "Audio failed. Read answer above.";
//         }
//     }}



using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Text;
using UnityEngine.Networking;

public class AIDoubtSolver : MonoBehaviour
{
    // ═══════════════════════════════════════════
    //  INSPECTOR FIELDS
    // ═══════════════════════════════════════════

    [Header("Gemini API")]
    public string apiKey = "AIzaSyDnv-GElQyA_4qjKDdEuoArurVL7AzRw5A";
    private string apiUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

    [Header("Layout Roots")]
    public RectTransform tutorialRoot;       // TutorialRoot object
    public GameObject    aiPanel;            // AIDrawer object
    public Button        floatingAskButton;  // AskAIButton

    [Header("Drawer UI")]
    public TMP_InputField  questionInputField;
    public TextMeshProUGUI answerText;
    public TextMeshProUGUI statusText;
    public Button          askButton;
    public Button          closeButton;
    public Button          listenButton;
    public Button          speakAnswerButton;

    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Teaching Manager")]
    public TeachingAudioManager teachingManager;

    // ═══════════════════════════════════════════
    //  PRIVATE VARIABLES
    // ═══════════════════════════════════════════
    private string    lastAnswer   = "";
    private bool      isRecording  = false;
    private bool      drawerOpen   = false;
    private Coroutine slideRoutine = null;
    private Coroutine pulseRoutine = null;

    private RectTransform drawerRT;

    const float DRAWER_WIDTH   = 380f;
    const float SLIDE_DURATION = 0.35f;

    // ═══════════════════════════════════════════
    //  START
    // ═══════════════════════════════════════════
    void Start()
    {
        // Get drawer RectTransform
        if (aiPanel != null)
            drawerRT = aiPanel.GetComponent<RectTransform>();

        // Set drawer starting position (hidden off screen to the right)
        InitDrawerPosition();

        // Apply visual design
        ApplyDesign();

        // Wire up all button clicks
        if (askButton != null)
            askButton.onClick.AddListener(OnAskButtonClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseDrawer);

        if (listenButton != null)
            listenButton.onClick.AddListener(StartVoiceInput);

        if (speakAnswerButton != null)
            speakAnswerButton.onClick.AddListener(SpeakAnswer);

        if (floatingAskButton != null)
            floatingAskButton.onClick.AddListener(OpenDrawer);

        if (questionInputField != null)
            questionInputField.onSubmit.AddListener((_) => OnAskButtonClicked());
    }

    // ═══════════════════════════════════════════
    //  DRAWER POSITION SETUP
    // ═══════════════════════════════════════════
    void InitDrawerPosition()
    {
        if (drawerRT == null) return;

        drawerRT.anchorMin        = new Vector2(1f, 0f);
        drawerRT.anchorMax        = new Vector2(1f, 1f);
        drawerRT.pivot            = new Vector2(1f, 0.5f);
        drawerRT.sizeDelta        = new Vector2(DRAWER_WIDTH, 0f);
        drawerRT.anchoredPosition = new Vector2(DRAWER_WIDTH, 0f); // hidden off screen
    }

    // ═══════════════════════════════════════════
    //  OPEN DRAWER  ← this is what button calls
    // ═══════════════════════════════════════════
    public void OpenDrawer()
    {
        if (drawerOpen) return;
        drawerOpen = true;

        // Pause the lesson
        if (teachingManager != null)
            teachingManager.PauseLesson();

        // Slide drawer in
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(AnimateSlide(open: true));

        // Set welcome message
        SetWelcomeText();
        SetStatus(StatusMode.Ready);

        // Focus input field
        if (questionInputField != null)
        {
            questionInputField.Select();
            questionInputField.ActivateInputField();
        }
    }

    // Keep old name working too
    public void OpenPanel() => OpenDrawer();

    // ═══════════════════════════════════════════
    //  CLOSE DRAWER
    // ═══════════════════════════════════════════
    public void CloseDrawer()
    {
        if (!drawerOpen) return;
        drawerOpen = false;

        // Resume the lesson
        if (teachingManager != null)
            teachingManager.ResumeLesson();

        // Stop audio
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        // Slide drawer out
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(AnimateSlide(open: false));

        isRecording = false;
    }

    // ═══════════════════════════════════════════
    //  SLIDE ANIMATION
    // ═══════════════════════════════════════════
    IEnumerator AnimateSlide(bool open)
    {
        float targetDrawerX   = open ? 0f : DRAWER_WIDTH;
        float targetTutorialR = open ? -DRAWER_WIDTH : 0f;

        float startDrawerX   = drawerRT != null ? drawerRT.anchoredPosition.x : DRAWER_WIDTH;
        float startTutorialR = tutorialRoot != null ? tutorialRoot.offsetMax.x : 0f;

        float elapsed = 0f;

        while (elapsed < SLIDE_DURATION)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / SLIDE_DURATION);

            // Move drawer
            if (drawerRT != null)
                drawerRT.anchoredPosition =
                    new Vector2(Mathf.Lerp(startDrawerX, targetDrawerX, t), 0f);

            // Shrink / expand tutorial
            if (tutorialRoot != null)
                tutorialRoot.offsetMax =
                    new Vector2(Mathf.Lerp(startTutorialR, targetTutorialR, t),
                                tutorialRoot.offsetMax.y);

            yield return null;
        }

        // Snap to final position
        if (drawerRT != null)
            drawerRT.anchoredPosition = new Vector2(targetDrawerX, 0f);

        if (tutorialRoot != null)
            tutorialRoot.offsetMax =
                new Vector2(targetTutorialR, tutorialRoot.offsetMax.y);
    }

    // ═══════════════════════════════════════════
    //  WELCOME TEXT
    // ═══════════════════════════════════════════
    void SetWelcomeText()
    {
        if (answerText != null)
            answerText.text =
                "<color=#00E5FF><b>Hi! I am your AI teacher.</b></color>\n\n" +
                "<color=#5A8AAA>Ask me anything about\nLogistic Regression!</color>";
    }

    // ═══════════════════════════════════════════
    //  STATUS BAR
    // ═══════════════════════════════════════════
    enum StatusMode { Ready, Thinking, Speaking, Listening, Error }

    void SetStatus(StatusMode mode)
    {
        if (statusText == null) return;

        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }

        switch (mode)
        {
            case StatusMode.Ready:
                statusText.text  = "● READY";
                statusText.color = HexColor("#00FF88");
                pulseRoutine     = StartCoroutine(PulseGreen());
                break;
            case StatusMode.Thinking:
                statusText.text  = "◌ THINKING...";
                statusText.color = HexColor("#FFD700");
                break;
            case StatusMode.Speaking:
                statusText.text  = "▶ SPEAKING";
                statusText.color = HexColor("#FF2FFF");
                break;
            case StatusMode.Listening:
                statusText.text  = "🎙 LISTENING";
                statusText.color = HexColor("#6C2FFF");
                break;
            case StatusMode.Error:
                statusText.text  = "✕ ERROR";
                statusText.color = HexColor("#FF4444");
                break;
        }
    }

    IEnumerator PulseGreen()
    {
        while (true)
        {
            yield return new WaitForSeconds(1.1f);
            if (statusText != null && statusText.text.StartsWith("●"))
            {
                statusText.color = new Color(0f, 1f, 0.53f, 0.3f);
                yield return new WaitForSeconds(0.3f);
                statusText.color = HexColor("#00FF88");
            }
        }
    }

    // ═══════════════════════════════════════════
    //  ASK BUTTON CLICKED
    // ═══════════════════════════════════════════
    public void OnAskButtonClicked()
    {
        if (questionInputField == null) return;

        string question = questionInputField.text.Trim();

        if (question == "")
        {
            if (statusText != null)
            {
                statusText.text  = "⚠ Type your question first!";
                statusText.color = HexColor("#FFD700");
            }
            return;
        }

        StartCoroutine(AskGemini(question));
        questionInputField.text = "";
    }

    // ═══════════════════════════════════════════
    //  GEMINI API CALL
    // ═══════════════════════════════════════════
    IEnumerator AskGemini(string question)
    {
        SetStatus(StatusMode.Thinking);

        if (answerText != null)
            answerText.text =
                "<color=#5A8AAA><i>Processing your question...</i></color>";

        if (askButton != null)
            askButton.interactable = false;

        string fullPrompt =
            "You are a helpful AI teacher explaining Logistic Regression. " +
            "The student is using an interactive Unity tutorial with a live sigmoid curve " +
            "f(x)=1/(1+e^(-(w*x+b))) and weight/bias sliders. " +
            "Answer clearly and friendly in max 60 words. No markdown symbols. " +
            "Student question: " + question;

        string jsonBody =
            "{\"contents\":[{\"parts\":[{\"text\":\"" +
            EscapeJson(fullPrompt) + "\"}]}]}";

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        string fullUrl = apiUrl + "?key=" + apiKey;

        using (UnityWebRequest req = new UnityWebRequest(fullUrl, "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("User-Agent",   "Unity");

            yield return req.SendWebRequest();

            if (askButton != null)
                askButton.interactable = true;

            if (req.result == UnityWebRequest.Result.Success)
            {
                string answer = ParseGeminiResponse(req.downloadHandler.text);
                lastAnswer = answer;

                if (answerText != null)
                    answerText.text = answer;

                SetStatus(StatusMode.Ready);
                StartCoroutine(SpeakWithTTS(answer));
            }
            else
            {
                Debug.LogError("Gemini Error: " + req.error);
                Debug.LogError("Response: " + req.downloadHandler.text);

                if (answerText != null)
                    answerText.text =
                        "<color=#FF6B6B>Connection failed.\n" +
                        "Check internet or API key.</color>";

                SetStatus(StatusMode.Error);
            }
        }
    }

    // ═══════════════════════════════════════════
    //  PARSE GEMINI RESPONSE
    // ═══════════════════════════════════════════
    string ParseGeminiResponse(string json)
    {
        try
        {
            int textIndex = json.IndexOf("\"text\":");
            if (textIndex == -1) return "Could not parse response.";

            int start = json.IndexOf("\"", textIndex + 7) + 1;
            int end   = json.IndexOf("\"", start);
            if (end <= start) return "Empty response received.";

            string text = json.Substring(start, end - start);
            text = text.Replace("\\n",  "\n")
                       .Replace("\\\"", "\"")
                       .Replace("\\\\", "\\");
            return text;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Parse error: " + e.Message);
            return "Sorry, I could not understand the response.";
        }
    }

    // ═══════════════════════════════════════════
    //  VOICE INPUT
    // ═══════════════════════════════════════════
    void StartVoiceInput()
    {
        if (!isRecording)
        {
            isRecording = true;
            SetStatus(StatusMode.Listening);

            TextMeshProUGUI btnText =
                listenButton != null
                ? listenButton.GetComponentInChildren<TextMeshProUGUI>()
                : null;

            if (btnText != null)
            {
                btnText.text  = "■ STOP";
                btnText.color = HexColor("#FF4444");
            }

            if (audioSource != null)
                audioSource.clip = Microphone.Start(null, false, 10, 44100);

            StartCoroutine(AutoStopRecording());
        }
        else
        {
            StopVoiceInput();
        }
    }

    IEnumerator AutoStopRecording()
    {
        yield return new WaitForSeconds(8f);
        if (isRecording) StopVoiceInput();
    }

    void StopVoiceInput()
    {
        isRecording = false;
        Microphone.End(null);

        TextMeshProUGUI btnText =
            listenButton != null
            ? listenButton.GetComponentInChildren<TextMeshProUGUI>()
            : null;

        if (btnText != null)
        {
            btnText.text  = "🎙 VOICE";
            btnText.color = HexColor("#D8F0FF");
        }

        if (statusText != null)
        {
            statusText.text  = "💡 Use Win+H to dictate!";
            statusText.color = HexColor("#FFD700");
        }
    }

    // ═══════════════════════════════════════════
    //  TEXT TO SPEECH
    // ═══════════════════════════════════════════
    void SpeakAnswer()
    {
        if (string.IsNullOrEmpty(lastAnswer)) return;
        StartCoroutine(SpeakWithTTS(lastAnswer));
    }

    IEnumerator SpeakWithTTS(string text)
    {
        SetStatus(StatusMode.Speaking);

        string ttsUrl =
            "https://translate.google.com/translate_tts?ie=UTF-8&q=" +
            UnityWebRequest.EscapeURL(text) + "&tl=en&client=tw-ob";

        using (UnityWebRequest req =
               UnityWebRequestMultimedia.GetAudioClip(ttsUrl, AudioType.MPEG))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                if (audioSource != null)
                {
                    audioSource.clip = DownloadHandlerAudioClip.GetContent(req);
                    audioSource.Play();
                    yield return new WaitUntil(() => !audioSource.isPlaying);
                }
            }
            else
            {
                Debug.LogWarning("TTS failed: " + req.error);
                if (statusText != null)
                {
                    statusText.text  = "Audio failed. Read answer above.";
                    statusText.color = HexColor("#FFD700");
                }
            }
        }

        SetStatus(StatusMode.Ready);
    }

    // ═══════════════════════════════════════════
    //  APPLY DESIGN
    // ═══════════════════════════════════════════
    void ApplyDesign()
    {
        // Panel background
        SetImg(aiPanel, HexColor("#0A1220"));

        // Answer card
        if (answerText != null)
        {
            var card = answerText.transform.parent?.gameObject;
            if (card != null) SetImg(card, HexColor("#0D1A2E"));
            answerText.color       = HexColor("#D8F0FF");
            answerText.fontSize    = 13;
            answerText.lineSpacing = 6f;
        }

        // Status text
        if (statusText != null)
        {
            statusText.fontSize         = 11;
            statusText.fontStyle        = FontStyles.Bold;
            statusText.characterSpacing = 2f;
        }

        // Input field
        if (questionInputField != null)
        {
            SetImg(questionInputField.gameObject, HexColor("#071018"));

            var ph = questionInputField.placeholder as TextMeshProUGUI;
            if (ph != null)
            {
                ph.text      = "Ask your doubt here...";
                ph.color     = HexColor("#1A3A55");
                ph.fontSize  = 12;
                ph.fontStyle = FontStyles.Italic;
            }

            var tc = questionInputField.textComponent as TextMeshProUGUI;
            if (tc != null)
            {
                tc.color    = HexColor("#D8F0FF");
                tc.fontSize = 13;
            }

            questionInputField.caretColor     = HexColor("#00E5FF");
            questionInputField.selectionColor =
                new Color(0f, 0.9f, 1f, 0.25f);
        }

        // Style buttons
        StyleBtn(askButton,         HexColor("#00E5FF"), HexColor("#060E18"), "ASK AI",   13);
        StyleBtn(listenButton,      HexColor("#6C2FFF"), HexColor("#D8F0FF"), "🎙 VOICE", 12);
        StyleBtn(speakAnswerButton, HexColor("#FF2FFF"), HexColor("#060E18"), "🔊 SPEAK", 12);
        StyleBtn(closeButton,       HexColor("#1A0828"), HexColor("#FF2FFF"), "✕  CLOSE", 12);

        // Style floating ask button
        StyleBtn(floatingAskButton, HexColor("#00E5FF"), HexColor("#060E18"), "✦  Ask AI", 13);
    }

    // ═══════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════
    void SetImg(GameObject go, Color color)
    {
        if (go == null) return;
        Image img = go.GetComponent<Image>();
        if (img == null) return;
        img.sprite = null;
        img.color  = color;
        img.type   = Image.Type.Simple;
    }

    void StyleBtn(Button btn, Color bg, Color textCol, string label, float fontSize)
    {
        if (btn == null) return;

        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = null;
            img.color  = bg;
            img.type   = Image.Type.Simple;
        }

        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text             = label;
            tmp.color            = textCol;
            tmp.fontSize         = fontSize;
            tmp.fontStyle        = FontStyles.Bold;
            tmp.alignment        = TextAlignmentOptions.Center;
            tmp.characterSpacing = 1.5f;
        }

        ColorBlock cb        = btn.colors;
        cb.normalColor       = bg;
        cb.highlightedColor  = new Color(bg.r + 0.1f, bg.g + 0.1f, bg.b + 0.1f, 1f);
        cb.pressedColor      = new Color(bg.r - 0.1f, bg.g - 0.1f, bg.b - 0.1f, 1f);
        cb.fadeDuration      = 0.08f;
        btn.colors           = cb;
    }

    static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}