// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using System.Collections;
// using System.Text;
// using UnityEngine.Networking;

// public class AIDoubtSolver : MonoBehaviour
// {
//     [Header("Gemini API")]
//     public string apiKey ="AIzaSyBz7n37OcIJO8IbTcLy8mF2fnfexaMLu3Q";
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
using System;

public class AIDoubtSolver : MonoBehaviour
{
    [Header("Gemini API")]
    public string apiKey = "";
    private string apiUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/" +
        "gemini-2.0-flash:generateContent";

    [Header("Screens")]
    public GameObject tutorialScreen;
    public GameObject chatbotScreen;

    [Header("Main Screen")]
    public Button askAIButton;

    [Header("Chatbot UI")]
    public TextMeshProUGUI chatHistoryText;
    public TMP_InputField  questionInputField;
    public TextMeshProUGUI statusText;
    public Button          sendButton;
    public Button          voiceButton;
    public Button          quitButton;
    public Button          speakButton;

    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Teaching Manager")]
    public TeachingAudioManager teachingManager;

    private string  lastAnswer  = "";
    private bool    isRecording = false;
    private AudioClip recordedClip = null;
    private string  chatHistory = "";

    const int SAMPLE_RATE     = 16000;
    const int MAX_RECORD_SECS = 10;

    // ═══════════════════════════════════════════
    //  START
    // ═══════════════════════════════════════════
    void Start()
    {
        // Show tutorial, hide chatbot at start
        ShowTutorial();

        // Wire buttons
        if (askAIButton != null)
            askAIButton.onClick.AddListener(
                OpenChatbot);

        if (sendButton != null)
            sendButton.onClick.AddListener(
                OnSendClicked);

        if (voiceButton != null)
            voiceButton.onClick.AddListener(
                ToggleVoice);

        if (quitButton != null)
            quitButton.onClick.AddListener(
                CloseChatbot);

        if (speakButton != null)
            speakButton.onClick.AddListener(
                SpeakLastAnswer);

        if (questionInputField != null)
            questionInputField.onSubmit
                .AddListener((_) =>
                    OnSendClicked());
    }

    // ═══════════════════════════════════════════
    //  SHOW TUTORIAL
    // ═══════════════════════════════════════════
    void ShowTutorial()
    {
        if (tutorialScreen != null)
            tutorialScreen.SetActive(true);
        if (chatbotScreen != null)
            chatbotScreen.SetActive(false);
    }

    // ═══════════════════════════════════════════
    //  OPEN CHATBOT
    // ═══════════════════════════════════════════
    public void OpenChatbot()
    {
        // Hide tutorial
        if (tutorialScreen != null)
            tutorialScreen.SetActive(false);

        // Show chatbot
        if (chatbotScreen != null)
            chatbotScreen.SetActive(true);

        // Pause lesson
        if (teachingManager != null)
            teachingManager.PauseLesson();

        // Reset chat
        chatHistory = "";
        UpdateChatDisplay(
            "<color=#00E5FF><b>AI Teacher</b>" +
            "</color>\n\n" +
            "Hi! I am your AI teacher.\n\n" +
            "You can:\n" +
            "● Type your question and press SEND\n" +
            "● Press VOICE to speak your question\n\n" +
            "Ask me anything about " +
            "Logistic Regression!");

        SetStatus("● READY");

        if (questionInputField != null)
        {
            questionInputField.text = "";
            questionInputField.Select();
            questionInputField.ActivateInputField();
        }
    }

    public void OpenPanel() => OpenChatbot();
    public void OpenDrawer() => OpenChatbot();

    // ═══════════════════════════════════════════
    //  CLOSE CHATBOT
    // ═══════════════════════════════════════════
    public void CloseChatbot()
    {
        // Stop recording if active
        if (isRecording)
        {
            Microphone.End(null);
            isRecording = false;
        }

        // Stop audio
        if (audioSource != null &&
            audioSource.isPlaying)
            audioSource.Stop();

        // Hide chatbot
        if (chatbotScreen != null)
            chatbotScreen.SetActive(false);

        // Show tutorial
        if (tutorialScreen != null)
            tutorialScreen.SetActive(true);

        // Resume lesson
        if (teachingManager != null)
            teachingManager.ResumeLesson();
    }

    // ═══════════════════════════════════════════
    //  SEND TEXT
    // ═══════════════════════════════════════════
    void OnSendClicked()
    {
        if (questionInputField == null) return;

        string question =
            questionInputField.text.Trim();

        if (question == "")
        {
            SetStatus("Please type a question!");
            return;
        }

        // Add student question to chat
        AddToChat(
            "<color=#00E5FF>You:</color> " +
            question);

        questionInputField.text = "";
        StartCoroutine(
            AskGeminiText(question));
    }

    // ═══════════════════════════════════════════
    //  VOICE TOGGLE
    // ═══════════════════════════════════════════
    void ToggleVoice()
    {
        if (!isRecording)
            StartRecording();
        else
            StopRecordingAndAsk();
    }

    void StartRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            SetStatus("No microphone found!");
            return;
        }

        isRecording  = true;
        recordedClip = Microphone.Start(
            null, false,
            MAX_RECORD_SECS, SAMPLE_RATE);

        SetStatus("🎙 Listening... click STOP when done");
        SetBtnText(voiceButton, "⏹ STOP");

        AddToChat(
            "<color=#6C2FFF>🎙 Recording..." +
            "</color>");

        StartCoroutine(AutoStop());
    }

    IEnumerator AutoStop()
    {
        yield return new WaitForSeconds(
            MAX_RECORD_SECS);
        if (isRecording)
            StopRecordingAndAsk();
    }

    void StopRecordingAndAsk()
    {
        if (!isRecording) return;
        isRecording = false;

        int lastSample =
            Microphone.GetPosition(null);
        Microphone.End(null);

        SetBtnText(voiceButton, "🎙 VOICE");
        SetStatus("Processing your voice...");

        if (recordedClip == null ||
            lastSample <= 0)
        {
            SetStatus("No audio! Try again.");
            return;
        }

        float[] samples =
            new float[lastSample *
            recordedClip.channels];
        recordedClip.GetData(samples, 0);

        byte[] wavBytes = ConvertToWav(
            samples,
            recordedClip.channels,
            SAMPLE_RATE);

        AddToChat(
            "<color=#6C2FFF>You (voice):</color>" +
            " [Sending to AI...]");

        StartCoroutine(
            AskGeminiAudio(wavBytes));
    }

    // ═══════════════════════════════════════════
    //  GEMINI TEXT
    // ═══════════════════════════════════════════
    IEnumerator AskGeminiText(string question)
    {
        SetStatus("Thinking...");

        if (sendButton != null)
            sendButton.interactable = false;

        string prompt =
            "You are a helpful AI teacher " +
            "explaining Logistic Regression. " +
            "The student is using an interactive " +
            "Unity tutorial with live sigmoid curve " +
            "and weight/bias sliders. " +
            "Answer clearly and friendly in " +
            "max 80 words. No markdown symbols. " +
            "Student question: " + question;

        string body =
            "{\"contents\":[{\"parts\":" +
            "[{\"text\":\"" +
            EscapeJson(prompt) +
            "\"}]}]}";

        yield return StartCoroutine(
            SendToGemini(
                Encoding.UTF8.GetBytes(body)));

        if (sendButton != null)
            sendButton.interactable = true;
    }

    // ═══════════════════════════════════════════
    //  GEMINI AUDIO
    // ═══════════════════════════════════════════
    IEnumerator AskGeminiAudio(byte[] wav)
    {
        SetStatus("Sending voice to AI...");

        string base64 =
            Convert.ToBase64String(wav);

        string prompt =
            "You are a helpful AI teacher " +
            "explaining Logistic Regression. " +
            "The student asked a question via voice. " +
            "Listen and answer clearly in " +
            "max 80 words. No markdown symbols.";

        string body =
            "{\"contents\":[{\"parts\":[" +
            "{\"inline_data\":{" +
            "\"mime_type\":\"audio/wav\"," +
            "\"data\":\"" + base64 + "\"}}," +
            "{\"text\":\"" +
            EscapeJson(prompt) +
            "\"}]}]}";

        yield return StartCoroutine(
            SendToGemini(
                Encoding.UTF8.GetBytes(body)));
    }

    // ═══════════════════════════════════════════
    //  SEND TO GEMINI
    // ═══════════════════════════════════════════
    IEnumerator SendToGemini(byte[] body)
    {
        string url = apiUrl + "?key=" + apiKey;

        using (UnityWebRequest req =
            new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler =
                new UploadHandlerRaw(body);
            req.downloadHandler =
                new DownloadHandlerBuffer();
            req.SetRequestHeader(
                "Content-Type",
                "application/json");

            yield return req.SendWebRequest();

            if (req.responseCode == 429)
            {
                AddToChat(
                    "<color=#FF4444>AI: " +
                    "Too many requests! " +
                    "Please wait 30 seconds " +
                    "and try again.</color>");
                SetStatus("Rate limit! Wait 30s.");
                yield break;
            }

            if (req.result ==
                UnityWebRequest.Result.Success)
            {
                string answer =
                    ParseResponse(
                        req.downloadHandler.text);
                lastAnswer = answer;

                AddToChat(
                    "<color=#00FF88>AI Teacher:" +
                    "</color> " + answer);

                SetStatus("● READY");
                StartCoroutine(Speak(answer));
            }
            else
            {
                Debug.LogError(req.error);
                Debug.LogError(
                    req.downloadHandler.text);

                AddToChat(
                    "<color=#FF4444>AI: " +
                    "Connection failed. " +
                    "Check internet!</color>");
                SetStatus("Error! Try again.");
            }
        }
    }

    // ═══════════════════════════════════════════
    //  PARSE RESPONSE
    // ═══════════════════════════════════════════
    string ParseResponse(string json)
    {
        try
        {
            int i = json.IndexOf("\"text\":");
            if (i == -1)
                return "No response received.";

            int s = json.IndexOf("\"", i+7) + 1;
            int e = json.IndexOf("\"", s);
            if (e <= s)
                return "Empty response.";

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

    // ═══════════════════════════════════════════
    //  CHAT DISPLAY
    // ═══════════════════════════════════════════
    void AddToChat(string message)
    {
        chatHistory += message + "\n\n";
        UpdateChatDisplay(chatHistory);
    }

    void UpdateChatDisplay(string text)
    {
        if (chatHistoryText != null)
            chatHistoryText.text = text;
    }

    // ═══════════════════════════════════════════
    //  TEXT TO SPEECH
    // ═══════════════════════════════════════════
    void SpeakLastAnswer()
    {
        if (string.IsNullOrEmpty(lastAnswer))
            return;
        StartCoroutine(Speak(lastAnswer));
    }

    IEnumerator Speak(string text)
    {
        SetStatus("▶ Speaking...");

        string url =
            "https://translate.google.com/" +
            "translate_tts?ie=UTF-8&q=" +
            UnityWebRequest.EscapeURL(text) +
            "&tl=en&client=tw-ob";

        using (UnityWebRequest req =
            UnityWebRequestMultimedia
            .GetAudioClip(url, AudioType.MPEG))
        {
            yield return req.SendWebRequest();

            if (req.result ==
                UnityWebRequest.Result.Success)
            {
                if (audioSource != null)
                {
                    audioSource.clip =
                        DownloadHandlerAudioClip
                        .GetContent(req);
                    audioSource.Play();
                    yield return new WaitUntil(
                        () => !audioSource.isPlaying);
                }
            }
        }

        SetStatus("● READY");
    }

    // ═══════════════════════════════════════════
    //  CONVERT TO WAV
    // ═══════════════════════════════════════════
    byte[] ConvertToWav(float[] samples,
        int channels, int sampleRate)
    {
        int byteCount = samples.Length * 2;
        int totalSize = 44 + byteCount;
        byte[] wav    = new byte[totalSize];

        Encoding.ASCII.GetBytes("RIFF")
            .CopyTo(wav, 0);
        BitConverter.GetBytes(totalSize - 8)
            .CopyTo(wav, 4);
        Encoding.ASCII.GetBytes("WAVE")
            .CopyTo(wav, 8);
        Encoding.ASCII.GetBytes("fmt ")
            .CopyTo(wav, 12);
        BitConverter.GetBytes(16)
            .CopyTo(wav, 16);
        BitConverter.GetBytes((short)1)
            .CopyTo(wav, 20);
        BitConverter.GetBytes((short)channels)
            .CopyTo(wav, 22);
        BitConverter.GetBytes(sampleRate)
            .CopyTo(wav, 24);
        BitConverter.GetBytes(
            sampleRate * channels * 2)
            .CopyTo(wav, 28);
        BitConverter.GetBytes(
            (short)(channels * 2))
            .CopyTo(wav, 32);
        BitConverter.GetBytes((short)16)
            .CopyTo(wav, 34);
        Encoding.ASCII.GetBytes("data")
            .CopyTo(wav, 36);
        BitConverter.GetBytes(byteCount)
            .CopyTo(wav, 40);

        int offset = 44;
        foreach (float s in samples)
        {
            short val = (short)(
                Mathf.Clamp(s, -1f, 1f)
                * short.MaxValue);
            BitConverter.GetBytes(val)
                .CopyTo(wav, offset);
            offset += 2;
        }

        return wav;
    }

    // ═══════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════
    void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
    }

    void SetBtnText(Button btn, string text)
    {
        if (btn == null) return;
        var tmp = btn
            .GetComponentInChildren
            <TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
    }

    static string EscapeJson(string s)
    {
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
