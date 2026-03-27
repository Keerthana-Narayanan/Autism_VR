using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using System;

// ================================================================
//  ChatbotUIBuilder  +  AIDoubtSolver  — all-in-one
//  Attach to ANY empty GameObject inside your Canvas.
//  Wire only 3 fields in Inspector:
//    - tutorialScreen   (drag TutorialScreen here)
//    - teachingManager  (drag AudioManager here)
//    - askAIButton      (drag your Ask AI button here)
// ================================================================
public class ChatbotUIBuilder : MonoBehaviour
{
    [Header("── Wire these 3 things only ──")]
    public GameObject           tutorialScreen;
    public TeachingAudioManager teachingManager;
    public Button               askAIButton;

    [Header("Chat Bubble Prefabs (recommended)")]
    [Tooltip("Drag Assets/Prefabs/UserBubblePrefab.prefab here")]
    public GameObject userBubblePrefabAsset;
    [Tooltip("Drag Assets/Prefabs/BotBubblePrefab.prefab here")]
    public GameObject botBubblePrefabAsset;

    [Header("Gemini API")]
    [Tooltip("Paste your Google AI Studio API key here. Get one at https://aistudio.google.com/app/apikey")]
    public string apiKey = "AIzaSyAG7-jKOjWNQZnnkdEHPhc9tbtMytEJV8M";

    [Header("UI Readability")]
    [Range(1.0f, 2.5f)]
    public float uiScale = 1.6f;

    // ── runtime refs ─────────────────────────────────────────────
    private GameObject    _chatScreen;
    private ScrollRect    _scrollRect;
    private RectTransform _contentArea;
    private TMP_InputField _inputField;
    private Button         _sendBtn;
    private Button         _voiceBtn;
    private GameObject     _typingIndicator;
    private AudioSource    _audioSource;
    private TextMeshProUGUI _badgeText;
    private Image           _badgeBg;

    private GameObject _userBubblePrefab;
    private GameObject _botBubblePrefab;

    // ── state ─────────────────────────────────────────────────────
    private string    _lastAnswer       = "";
    private bool      _isRecording      = false;
    private AudioClip _recordedClip     = null;
    private bool      _isRequestRunning = false;
    private float     _lastRequestTime  = -999f;
    private float     _requestCooldown  = 5f;
    private Coroutine _countdownCo      = null;

    private bool      _openedByAttention     = false;
    private string    _lastAttentionState     = "";
    private float     _lastAttentionChangeTime = -999f;
    public  float     attentionDebounceSeconds = 1.0f;

    private readonly List<Button> _suggestionButtons = new List<Button>();

    private string _apiUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/" +
        "gemini-2.5-flash-lite:generateContent";

    private const string SYSTEM_PROMPT =
        "You are an AI Teacher for a VR Machine Learning course. " +
        "You help students understand ML concepts clearly and warmly. " +
        "You are an expert in these topics: " +
        "logistic regression, linear regression, decision trees, random forests, " +
        "support vector machines, neural networks, deep learning, " +
        "sigmoid function, activation functions, loss functions, " +
        "gradient descent, backpropagation, overfitting, underfitting, " +
        "bias, variance, regularization (L1/L2), dropout, " +
        "train/test split, cross-validation, confusion matrix, " +
        "accuracy, precision, recall, F1 score, ROC curve, AUC, " +
        "feature engineering, normalization, standardization, " +
        "PCA, clustering, k-means, KNN, naive bayes, " +
        "reinforcement learning, supervised learning, unsupervised learning, " +
        "convolutional neural networks, recurrent neural networks, transformers, " +
        "attention mechanism, embeddings, tokenization, " +
        "data preprocessing, missing values, class imbalance, " +
        "hyperparameter tuning, learning rate, batch size, epochs. " +
        "If the student asks ANYTHING not related to machine learning or data science, " +
        "reply ONLY with: " +
        "'I am your ML Teacher! I can only help with Machine Learning and Data Science topics. " +
        "Please ask a study question!' " +
        "Keep answers under 80 words. No markdown symbols. Be warm and encouraging. ";

    const int SAMPLE_RATE     = 16000;
    const int MAX_RECORD_SECS = 10;

    // ════════════════════════════════════════════════════════════
    void Start()
    {
        _audioSource = gameObject.GetComponent<AudioSource>()
                    ?? gameObject.AddComponent<AudioSource>();
        BuildUI();
        if (askAIButton != null)
            askAIButton.onClick.AddListener(OpenChatbot);
        ShowTutorial();
    }

    // ════════════════════════════════════════════════════════════
    //  SPRITE HELPER
    // ════════════════════════════════════════════════════════════
    static Sprite _roundedSprite;

    static Sprite GetRoundedSprite()
    {
        if (_roundedSprite != null) return _roundedSprite;

        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        _roundedSprite = Sprite.Create(
            tex,
            new Rect(0, 0, 4, 4),
            new Vector2(0.5f, 0.5f),
            1f, 1u,
            SpriteMeshType.FullRect,
            new Vector4(1, 1, 1, 1));
        return _roundedSprite;
    }

    static void ApplyRounded(Image img)
    {
        if (img == null) return;
        img.sprite = GetRoundedSprite();
        img.type   = Image.Type.Sliced;
    }

    float SU(float value) => value * uiScale;
    int SI(int value) => Mathf.RoundToInt(value * uiScale);

    // ════════════════════════════════════════════════════════════
    //  BUILD UI
    // ════════════════════════════════════════════════════════════
    void BuildUI()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("[ChatbotUI] No Canvas found!"); return; }

        Transform old = canvas.transform.Find("ChatbotScreen");
        if (old != null) Destroy(old.gameObject);

        _chatScreen = CreatePanel(canvas.transform, "ChatbotScreen",
                                  Hex("0A0A14"),
                                  Vector2.zero, Vector2.one,
                                  Vector2.zero, Vector2.zero);
        _chatScreen.SetActive(false);

        BuildHeader();
        BuildDivider(_chatScreen.transform, -64f, -63f);
        BuildScrollView();
        BuildTypingIndicator();
        BuildInputBar();
        BuildBubblePrefabs();   // FIX 3: was missing — caused null prefab on Instantiate
    }

    // ────────────────────────────────────────────────────────────
    //  HEADER
    // ────────────────────────────────────────────────────────────
    void BuildHeader()
    {
        var header = CreatePanel(_chatScreen.transform, "Header",
                                 Hex("100B28"),
                                 new Vector2(0, 1), new Vector2(1, 1),
                                 new Vector2(0, -SU(92)), new Vector2(0, 0));

        // avatar circle
        var av    = MakeGO("Avatar", header.transform);
        var avImg = av.AddComponent<Image>();
        avImg.color = Hex("7C3AED");
        var avRT = av.GetComponent<RectTransform>();
        avRT.anchorMin = new Vector2(0, 0.5f); avRT.anchorMax = new Vector2(0, 0.5f);
        avRT.pivot     = new Vector2(0, 0.5f);
        avRT.anchoredPosition = new Vector2(SU(18), 0);
        avRT.sizeDelta = new Vector2(SU(52), SU(52));
        var avTxt = MakeTMP(av.transform, "AI", SU(18), Color.white, true);
        StretchFill(avTxt.GetComponent<RectTransform>());
        avTxt.alignment = TextAlignmentOptions.Center;

        // title
        var title = MakeTMP(header.transform, "AI Teacher", SU(22), Hex("E2D9F3"), true);
        var tRT   = title.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0, 0.5f); tRT.anchorMax = new Vector2(0, 0.5f);
        tRT.pivot = new Vector2(0, 0.5f);
        tRT.anchoredPosition = new Vector2(SU(84), SU(14));
        tRT.sizeDelta = new Vector2(SU(360), SU(30));

        // subtitle
        var sub = MakeTMP(header.transform, "ML Learning Assistant", SU(14), Hex("9F7AEA"), false);
        var sRT = sub.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0, 0.5f); sRT.anchorMax = new Vector2(0, 0.5f);
        sRT.pivot = new Vector2(0, 0.5f);
        sRT.anchoredPosition = new Vector2(SU(84), -SU(14));
        sRT.sizeDelta = new Vector2(SU(380), SU(24));

        // focus badge
        var badge = MakeGO("Badge", header.transform);
        _badgeBg  = badge.AddComponent<Image>();
        _badgeBg.color = new Color(0.08f, 0.35f, 0.12f, 0.85f);
        var bRT = badge.GetComponent<RectTransform>();
        bRT.anchorMin = new Vector2(1, 0.5f); bRT.anchorMax = new Vector2(1, 0.5f);
        bRT.pivot = new Vector2(1, 0.5f);
        bRT.anchoredPosition = new Vector2(-SU(74), 0);
        bRT.sizeDelta = new Vector2(SU(140), SU(34));
        _badgeText = MakeTMP(badge.transform, "● FOCUSED", SU(13), Hex("4ADE80"), true);
        StretchFill(_badgeText.GetComponent<RectTransform>());
        _badgeText.alignment = TextAlignmentOptions.Center;

        // close button
        var closeGO  = MakeGO("CloseBtn", header.transform);
        var closeImg = closeGO.AddComponent<Image>();
        closeImg.color = Hex("1A1535");
        var cRT = closeGO.GetComponent<RectTransform>();
        cRT.anchorMin = new Vector2(1, 0.5f); cRT.anchorMax = new Vector2(1, 0.5f);
        cRT.pivot = new Vector2(1, 0.5f);
        cRT.anchoredPosition = new Vector2(-SU(14), 0);
        cRT.sizeDelta = new Vector2(SU(40), SU(40));
        var closeBtn = closeGO.AddComponent<Button>();
        var cTxt = MakeTMP(closeGO.transform, "X", SU(20), Hex("9F7AEA"), true);
        StretchFill(cTxt.GetComponent<RectTransform>());
        cTxt.alignment = TextAlignmentOptions.Center;
        closeBtn.onClick.AddListener(CloseChatbot);
    }

    // ────────────────────────────────────────────────────────────
    //  DIVIDER
    // ────────────────────────────────────────────────────────────
    void BuildDivider(Transform parent, float top, float bottom)
    {
        var d   = MakeGO("Divider", parent);
        var img = d.AddComponent<Image>();
        img.color = Hex("2D1F5E");
        var rt  = d.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = new Vector2(0, top);
        rt.offsetMax = new Vector2(0, bottom);
    }

    // ────────────────────────────────────────────────────────────
    //  SCROLL VIEW
    // ────────────────────────────────────────────────────────────
    void BuildScrollView()
    {
        var svGO  = MakeGO("ScrollView", _chatScreen.transform);
        var svImg = svGO.AddComponent<Image>();
        svImg.color = Hex("0A0A14");
        ApplyRounded(svImg);
        var svRT = svGO.GetComponent<RectTransform>();
        svRT.anchorMin = new Vector2(0, 0);
        svRT.anchorMax = new Vector2(1, 1);
        svRT.offsetMin = new Vector2(0, SU(108));
        svRT.offsetMax = new Vector2(0, -SU(92));

        var sr = svGO.AddComponent<ScrollRect>();
        sr.horizontal       = false;
        sr.vertical         = true;
        sr.movementType     = ScrollRect.MovementType.Elastic;
        sr.elasticity       = 0.1f;
        sr.inertia          = true;
        sr.decelerationRate = 0.135f;
        _scrollRect = sr;

        // viewport
        var vpGO  = MakeGO("Viewport", svGO.transform);
        var vpImg = vpGO.AddComponent<Image>();
        vpImg.color = Color.clear;
        var vpMask = vpGO.AddComponent<Mask>();
        vpMask.showMaskGraphic = false;
        vpGO.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        var vpRT = vpGO.GetComponent<RectTransform>();
        StretchFill(vpRT);
        sr.viewport = vpRT;

        // content
        var cGO = MakeGO("ContentArea", vpGO.transform);
        _contentArea = cGO.GetComponent<RectTransform>();
        if (_contentArea == null)
            _contentArea = cGO.AddComponent<RectTransform>();
        _contentArea.anchorMin        = new Vector2(0, 1);
        _contentArea.anchorMax        = new Vector2(1, 1);
        _contentArea.pivot            = new Vector2(0.5f, 1);
        _contentArea.anchoredPosition = Vector2.zero;
        _contentArea.sizeDelta        = Vector2.zero;

        var vlg = cGO.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment     = TextAnchor.UpperLeft;
        vlg.spacing            = SU(14);
        vlg.padding            = new RectOffset(SI(18), SI(18), SI(20), SI(20));
        vlg.childControlWidth  = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = true;

        var csf = cGO.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = _contentArea;
    }

    // ────────────────────────────────────────────────────────────
    //  TYPING INDICATOR
    // ────────────────────────────────────────────────────────────
    void BuildTypingIndicator()
    {
        _typingIndicator = MakeGO("TypingIndicator", _chatScreen.transform);
        var rt = _typingIndicator.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
        rt.pivot     = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, SU(108));
        rt.sizeDelta = new Vector2(0, SU(54));

        var hlg = _typingIndicator.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment     = TextAnchor.MiddleLeft;
        hlg.spacing            = SU(10);
        hlg.padding            = new RectOffset(SI(20), SI(20), SI(8), SI(8));
        hlg.childControlWidth  = false; hlg.childControlHeight  = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        var dot    = MakeGO("Dot", _typingIndicator.transform);
        var dotImg = dot.AddComponent<Image>();
        dotImg.color = Hex("7C3AED");
        dot.GetComponent<RectTransform>().sizeDelta = new Vector2(SU(34), SU(34));
        var dl = MakeTMP(dot.transform, "AI", SU(11), Color.white, true);
        StretchFill(dl.GetComponent<RectTransform>());
        dl.alignment = TextAlignmentOptions.Center;

        var bub    = MakeGO("Bubble", _typingIndicator.transform);
        var bubImg = bub.AddComponent<Image>();
        bubImg.color = Hex("1A1040");
        ApplyRounded(bubImg);
        bub.GetComponent<RectTransform>().sizeDelta = new Vector2(SU(88), SU(42));
        var bhlg = bub.AddComponent<HorizontalLayoutGroup>();
        bhlg.childAlignment = TextAnchor.MiddleCenter;
        bhlg.spacing = 5;
        bhlg.padding = new RectOffset(10, 10, 8, 8);
        bhlg.childControlWidth = false; bhlg.childControlHeight = false;
        bhlg.childForceExpandWidth = false; bhlg.childForceExpandHeight = false;

        var dots = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var d = MakeGO("D" + i, bub.transform);
            dots[i] = d.AddComponent<Image>();
            dots[i].color = Hex("7C3AED");
            d.GetComponent<RectTransform>().sizeDelta = new Vector2(7, 7);
        }
        _typingIndicator.AddComponent<TypingDotsAnim>().Init(dots[0], dots[1], dots[2]);
        _typingIndicator.SetActive(false);
    }

    // ────────────────────────────────────────────────────────────
    //  INPUT BAR  (text field + MIC + Send)
    // ────────────────────────────────────────────────────────────
    void BuildInputBar()
    {
        var bar = CreatePanel(_chatScreen.transform, "InputBar",
                      Hex("100B28"),
                      new Vector2(0, 0), new Vector2(1, 0),
                      new Vector2(0, 0), new Vector2(0, SU(100)));

        // top divider
        var div = MakeGO("Div", bar.transform);
        div.AddComponent<Image>().color = Hex("2D1F5E");
        var dRT = div.GetComponent<RectTransform>();
        dRT.anchorMin = new Vector2(0, 1); dRT.anchorMax = new Vector2(1, 1);
        dRT.pivot     = new Vector2(0.5f, 1);
        dRT.anchoredPosition = Vector2.zero;
        dRT.sizeDelta = new Vector2(0, 1);

        // input background pill
        var bg    = MakeGO("InputBg", bar.transform);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = Hex("1A1040");
        ApplyRounded(bgImg);
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.5f); bgRT.anchorMax = new Vector2(1, 0.5f);
        bgRT.pivot     = new Vector2(0.5f, 0.5f);
        bgRT.offsetMin = new Vector2(SU(18), -SU(30));
        bgRT.offsetMax = new Vector2(-SU(150), SU(30));

        // ── TMP_InputField ───────────────────────────────────────
        var ifGO  = MakeGO("InputField", bar.transform);
        var ifImg = ifGO.AddComponent<Image>();
        ifImg.color = Color.clear;
        var ifRT = ifGO.GetComponent<RectTransform>();
        ifRT.anchorMin = new Vector2(0, 0.5f); ifRT.anchorMax = new Vector2(1, 0.5f);
        ifRT.pivot     = new Vector2(0.5f, 0.5f);
        ifRT.offsetMin = new Vector2(SU(18), -SU(30));
        ifRT.offsetMax = new Vector2(-SU(150), SU(30));

        // TextArea (viewport)
        var ta   = MakeGO("TextArea", ifGO.transform);
        var taRT = ta.GetComponent<RectTransform>();    // FIX 1: GetComponent — MakeGO already adds RectTransform
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(SU(16), SU(6)); taRT.offsetMax = new Vector2(-SU(16), -SU(6));
        ta.AddComponent<RectMask2D>();

        // placeholder
        var ph  = MakeGO("Placeholder", ta.transform);
        var phT = ph.AddComponent<TextMeshProUGUI>();
        phT.text      = "Ask me anything about ML...";
        phT.fontSize  = SU(19);
        phT.color     = Hex("4A3F7B");
        phT.fontStyle = FontStyles.Italic;
        StretchFill(ph.GetComponent<RectTransform>());

        // input text
        var tx  = MakeGO("Text", ta.transform);
        var txT = tx.AddComponent<TextMeshProUGUI>();
        txT.fontSize = SU(19);
        txT.color    = Hex("E2D9F3");
        StretchFill(tx.GetComponent<RectTransform>());

        // wire TMP_InputField AFTER creating children
        _inputField = ifGO.AddComponent<TMP_InputField>();
        _inputField.textViewport   = taRT;
        _inputField.textComponent  = txT;
        _inputField.placeholder    = phT;
        _inputField.caretColor     = Hex("9F7AEA");
        _inputField.selectionColor = new Color(0.49f, 0.23f, 0.93f, 0.35f);
        _inputField.onSubmit.AddListener((_) => OnSendClicked());

        // ── Voice (MIC) button ───────────────────────────────────
        var vGO  = MakeGO("VoiceBtn", bar.transform);
        var vImg = vGO.AddComponent<Image>();
        vImg.color = Hex("1A1535");
        ApplyRounded(vImg);
        var vRT = vGO.GetComponent<RectTransform>();
        vRT.anchorMin = new Vector2(1, 0.5f); vRT.anchorMax = new Vector2(1, 0.5f);
        vRT.pivot     = new Vector2(1, 0.5f);
        vRT.anchoredPosition = new Vector2(-SU(82), 0);
        vRT.sizeDelta = new Vector2(SU(62), SU(62));
        _voiceBtn = vGO.AddComponent<Button>();
        _voiceBtn.targetGraphic = vImg;
        var vT = MakeTMP(vGO.transform, "MIC", SU(13), Hex("9F7AEA"), true);
        StretchFill(vT.GetComponent<RectTransform>());
        vT.alignment = TextAlignmentOptions.Center;
        _voiceBtn.onClick.AddListener(ToggleVoice);

        // ── Send button ──────────────────────────────────────────
        var sGO  = MakeGO("SendBtn", bar.transform);
        var sImg = sGO.AddComponent<Image>();
        sImg.color = Hex("7C3AED");
        ApplyRounded(sImg);
        var sRT = sGO.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(1, 0.5f); sRT.anchorMax = new Vector2(1, 0.5f);
        sRT.pivot     = new Vector2(1, 0.5f);
        sRT.anchoredPosition = new Vector2(-SU(12), 0);
        sRT.sizeDelta = new Vector2(SU(62), SU(62));
        _sendBtn = sGO.AddComponent<Button>();
        _sendBtn.targetGraphic = sImg;
        var sT = MakeTMP(sGO.transform, "Send", SU(14), Color.white, true);
        StretchFill(sT.GetComponent<RectTransform>());
        sT.alignment = TextAlignmentOptions.Center;
        _sendBtn.onClick.AddListener(OnSendClicked);
    }

    // ────────────────────────────────────────────────────────────
    //  BUBBLE PREFABS
    // ────────────────────────────────────────────────────────────
    void BuildBubblePrefabs()
    {
        // Prefer external prefab assets if provided in Inspector.
        _userBubblePrefab = PrepareBubbleTemplate(userBubblePrefabAsset, "UserBubbleTemplate");
        _botBubblePrefab  = PrepareBubbleTemplate(botBubblePrefabAsset,  "BotBubbleTemplate");

        // Fallback to generated templates when prefabs are not wired/invalid.
        if (_userBubblePrefab == null)
            _userBubblePrefab = BuildUserBubble();
        if (_botBubblePrefab == null)
            _botBubblePrefab = BuildBotBubble();
    }

    GameObject PrepareBubbleTemplate(GameObject source, string templateName)
    {
        if (source == null) return null;

        var clone = Instantiate(source);
        clone.name = templateName;
        clone.SetActive(false);
        DontDestroyOnLoad(clone);

        // Prefabs can carry nested Canvas objects or zero scales from authoring scene.
        // Normalize so they render correctly inside the chat content area.
        foreach (var t in clone.GetComponentsInChildren<Transform>(true))
        {
            var rt = t as RectTransform;
            t.localScale = Vector3.one;
            t.localRotation = Quaternion.identity;
            if (rt != null) rt.localPosition = Vector3.zero;
        }

        foreach (var c in clone.GetComponentsInChildren<Canvas>(true))
            if (c.gameObject != clone) Destroy(c);
        foreach (var cs in clone.GetComponentsInChildren<CanvasScaler>(true))
            Destroy(cs);
        foreach (var gr in clone.GetComponentsInChildren<GraphicRaycaster>(true))
            Destroy(gr);

        // Must contain a message text target.
        if (!ContainsMessageText(clone.transform))
        {
            Debug.LogWarning("[ChatbotUI] Prefab " + source.name + " has no MessageText child. Falling back to generated bubble.");
            Destroy(clone);
            return null;
        }

        return clone;
    }

    bool ContainsMessageText(Transform root)
    {
        if (root == null) return false;
        var direct = root.Find("Bubble/MessageText");
        if (direct != null) return true;
        foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (t != null && t.gameObject.name == "MessageText") return true;
        return false;
    }

    GameObject BuildUserBubble()
    {
        var root = MakeGO("UserBubble", null);
        root.SetActive(false);
        DontDestroyOnLoad(root);

        // Root: full-width row, right-aligned, auto height
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0, 1);
        rootRT.anchorMax = new Vector2(1, 1);
        rootRT.sizeDelta = new Vector2(0, 0);

        var hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment        = TextAnchor.UpperRight;
        hlg.padding               = new RectOffset(SI(70), SI(14), SI(6), SI(6));
        hlg.childControlWidth     = true;
        hlg.childControlHeight    = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var rootCsf = root.AddComponent<ContentSizeFitter>();
        rootCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rootCsf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Bubble background — sizes itself to text
        var bub    = MakeGO("Bubble", root.transform);
        var bubImg = bub.AddComponent<Image>();
        bubImg.color = Hex("6D28D9");
        ApplyRounded(bubImg);

        var bhlg = bub.AddComponent<HorizontalLayoutGroup>();
        bhlg.padding              = new RectOffset(SI(18), SI(18), SI(14), SI(14));
        bhlg.childControlWidth    = true;
        bhlg.childControlHeight   = true;
        bhlg.childForceExpandWidth  = false;
        bhlg.childForceExpandHeight = false;

        var bcsf = bub.AddComponent<ContentSizeFitter>();
        bcsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        bcsf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Message text — NO fixed sizeDelta; let TMP measure itself
        var msg = MakeGO("MessageText", bub.transform);
        var mt  = msg.AddComponent<TextMeshProUGUI>();
        mt.fontSize          = SU(19);
        mt.color             = Color.white;
        mt.textWrappingMode  = TextWrappingModes.Normal;
        mt.overflowMode      = TextOverflowModes.Overflow;
        mt.textWrappingMode = TextWrappingModes.Normal;
        // Max width = ~60% of chat area; TMP wraps within this
        var msgRT = msg.GetComponent<RectTransform>();
        msgRT.sizeDelta = new Vector2(SU(360),0);

        var msgLe = msg.AddComponent<LayoutElement>();
        msgLe.minWidth = SU(140);
        msgLe.flexibleWidth  = 0;
        msgLe.preferredWidth = SU(360);

        return root;
    }

    GameObject BuildBotBubble()
    {
        var root = MakeGO("BotBubble", null);
        root.SetActive(false);
        DontDestroyOnLoad(root);

        // Root: full-width row, left-aligned, auto height
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0, 1);
        rootRT.anchorMax = new Vector2(1, 1);
        rootRT.sizeDelta = new Vector2(0, 0);

        var hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment        = TextAnchor.UpperLeft;
        hlg.padding               = new RectOffset(SI(14), SI(70), SI(6), SI(6));
        hlg.spacing               = SU(10);
        hlg.childControlWidth     = true;
        hlg.childControlHeight    = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var rootCsf = root.AddComponent<ContentSizeFitter>();
        rootCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rootCsf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Avatar circle — fixed 28x28, does not grow
        var av    = MakeGO("Avatar", root.transform);
        var avImg = av.AddComponent<Image>();
        avImg.color = Hex("7C3AED");
        var avLe = av.AddComponent<LayoutElement>();
        avLe.minWidth    = SU(34); avLe.preferredWidth  = SU(34);
        avLe.minHeight   = SU(34); avLe.preferredHeight = SU(34);
        avLe.flexibleWidth = 0;
        var avT = MakeTMP(av.transform, "AI", SU(11), Color.white, true);
        StretchFill(avT.GetComponent<RectTransform>());
        avT.alignment = TextAlignmentOptions.Center;

        // Bubble background — sizes itself to text
        var bub    = MakeGO("Bubble", root.transform);
        var bubImg = bub.AddComponent<Image>();
        bubImg.color = Hex("1A1040");
        ApplyRounded(bubImg);

        var bhlg = bub.AddComponent<HorizontalLayoutGroup>();
        bhlg.padding              = new RectOffset(SI(18), SI(18), SI(14), SI(14));
        bhlg.childControlWidth    = true;
        bhlg.childControlHeight   = true;
        bhlg.childForceExpandWidth  = false;
        bhlg.childForceExpandHeight = false;

        var bcsf = bub.AddComponent<ContentSizeFitter>();
        bcsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        bcsf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Message text — NO fixed sizeDelta; let TMP measure itself
        var msg = MakeGO("MessageText", bub.transform);
        var mt  = msg.AddComponent<TextMeshProUGUI>();
        mt.fontSize          = SU(19);
        mt.color             = Hex("D4C9F0");
        mt.textWrappingMode  = TextWrappingModes.Normal;
        mt.overflowMode      = TextOverflowModes.Overflow;
        var msgRT = msg.GetComponent<RectTransform>();
        msgRT.sizeDelta = new Vector2(SU(380), 0);

        var msgLe = msg.AddComponent<LayoutElement>();
        msgLe.minWidth = SU(140);
        msgLe.flexibleWidth  = 0;
        msgLe.preferredWidth = SU(380);

        return root;
    }

    // ════════════════════════════════════════════════════════════
    //  OPEN / CLOSE
    // ════════════════════════════════════════════════════════════
    public void OpenChatbot()
    {
        OpenChatbotInternal(
            openedByAttention: false,
            greetingText:
                "Hi! I am your AI Teacher for Machine Learning.\n\n" +
                "Ask me anything about ML concepts — " +
                "logistic regression, neural networks, gradient descent, " +
                "decision trees, and more!\n\n" +
                "Type your question or press MIC to speak."
        );
    }

    public void CloseChatbot()
    {
        CloseChatbotInternal(resumeLesson: true, showTutorial: true);
    }

    void OpenChatbotInternal(bool openedByAttention, string greetingText)
    {
        if (tutorialScreen != null) tutorialScreen.SetActive(false);
        _chatScreen.SetActive(true);
        if (teachingManager != null) teachingManager.PauseLesson();

        _openedByAttention = openedByAttention;
        ClearChat();
        SetSuggestionsInteractable(true);

        StartCoroutine(SpawnMetaLine("TODAY : SESSION 1", 0.05f));
        StartCoroutine(SpawnBotBubble(greetingText, 0.3f));
        StartCoroutine(SpawnSuggestionChip("What is the sigmoid function and why do we use it?", 0.7f));
        StartCoroutine(SpawnSuggestionChip("How does the decision boundary work?", 1.0f));
        StartCoroutine(SpawnSuggestionChip("Explain gradient descent in simple words.", 1.3f));
    }

    void CloseChatbotInternal(bool resumeLesson, bool showTutorial)
    {
        if (_isRecording) { Microphone.End(null); _isRecording = false; }
        if (_audioSource != null && _audioSource.isPlaying) _audioSource.Stop();
        if (_countdownCo != null) { StopCoroutine(_countdownCo); _countdownCo = null; }
        if (_typingIndicator != null) _typingIndicator.SetActive(false);

        _chatScreen.SetActive(false);
        _openedByAttention = false;

        if (showTutorial && tutorialScreen != null)
            tutorialScreen.SetActive(true);
        if (resumeLesson && teachingManager != null)
            teachingManager.ResumeLesson();
    }

    void ShowTutorial()
    {
        if (tutorialScreen != null) tutorialScreen.SetActive(true);
        if (_chatScreen != null)    _chatScreen.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════
    //  ATTENTION STATE
    // ════════════════════════════════════════════════════════════
    string NormalizeAttentionState(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        raw = raw.Trim().ToUpperInvariant();
        if (raw.Contains("UNFO"))  return "UNFOCUSED";
        if (raw.Contains("DRO"))   return "DROWSY";
        if (raw.Contains("FOCUS")) return "FOCUSED";
        return raw;
    }

    public void SetFocusState(string state)
    {
        if (_badgeText == null || _badgeBg == null) return;
        string norm = NormalizeAttentionState(state);
        if (string.IsNullOrEmpty(norm)) return;

        switch (norm)
        {
            case "FOCUSED":
                _badgeText.text  = "● FOCUSED";
                _badgeText.color = Hex("4ADE80");
                _badgeBg.color   = new Color(0.08f, 0.35f, 0.12f, 0.85f);
                break;
            case "UNFOCUSED":
                _badgeText.text  = "● UNFOCUSED";
                _badgeText.color = Hex("FACC15");
                _badgeBg.color   = new Color(0.35f, 0.28f, 0.04f, 0.85f);
                break;
            case "DROWSY":
                _badgeText.text  = "● DROWSY";
                _badgeText.color = Hex("F87171");
                _badgeBg.color   = new Color(0.35f, 0.06f, 0.06f, 0.85f);
                break;
        }

        bool changed = norm != _lastAttentionState;
        if (!changed) return;

        float now = Time.time;
        bool inDebounceWindow =
            _lastAttentionChangeTime > -100f &&
            (now - _lastAttentionChangeTime) < attentionDebounceSeconds;
        if (norm != "DROWSY" && inDebounceWindow) return;

        _lastAttentionState      = norm;
        _lastAttentionChangeTime = now;

        bool chatOpen = _chatScreen != null && _chatScreen.activeSelf;

        switch (norm)
        {
            case "FOCUSED":
                if (teachingManager != null && teachingManager.IsInBreak && !chatOpen)
                    teachingManager.ResumeLesson();
                if (_openedByAttention && chatOpen)
                    CloseChatbotInternal(resumeLesson: true, showTutorial: true);
                else if (!chatOpen && tutorialScreen != null)
                    tutorialScreen.SetActive(true);
                break;

            case "UNFOCUSED":
                if (!chatOpen)
                    OpenChatbotInternal(
                        openedByAttention: true,
                        greetingText: "I noticed you might be a little unfocused. What part of the lesson is confusing? Ask me and I'll help.");
                break;

            case "DROWSY":
                if (chatOpen)
                    CloseChatbotInternal(resumeLesson: false, showTutorial: false);
                _openedByAttention = false;
                if (tutorialScreen != null) tutorialScreen.SetActive(false);
                if (teachingManager != null) teachingManager.BreakLesson();
                SetSuggestionsInteractable(false);
                break;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  SEND TEXT
    // ════════════════════════════════════════════════════════════
    void OnSendClicked()
    {
        float remaining = _requestCooldown - (Time.time - _lastRequestTime);
        if (remaining > 0f)
        {
            if (_countdownCo == null)
            {
                SetSuggestionsInteractable(false);
                _countdownCo = StartCoroutine(Countdown(remaining));
            }
            return;
        }
        if (_isRequestRunning || _inputField == null) return;

        string q = _inputField.text.Trim();
        if (q == "") return;

        SpawnUserBubble(q);
        _inputField.text = "";
        _isRequestRunning     = true;
        _lastRequestTime      = Time.time;
        _sendBtn.interactable = false;
        SetSuggestionsInteractable(false);
        ShowTyping(true);
        StartCoroutine(AskGemini(q));
    }

    IEnumerator Countdown(float secs)
    {
        _sendBtn.interactable = false;
        float end = Time.time + secs;
        while (Time.time < end) yield return new WaitForSeconds(0.25f);
        _sendBtn.interactable = true;
        SetSuggestionsInteractable(true);
        _countdownCo = null;
    }

    // ════════════════════════════════════════════════════════════
    //  VOICE
    // ════════════════════════════════════════════════════════════
    void ToggleVoice()
    {
        if (!_isRecording) StartVoice();
        else               StopVoice();
    }

    void StartVoice()
    {
        if (Microphone.devices.Length == 0)
        {
            StartCoroutine(SpawnBotBubble("No microphone found on this device.", 0f));
            return;
        }
        _isRecording  = true;
        _recordedClip = Microphone.Start(null, false, MAX_RECORD_SECS, SAMPLE_RATE);
        SetVoiceColor(true);
        SpawnUserBubble("🎤 Recording... tap MIC again to stop.");
        StartCoroutine(AutoStopVoice());
    }

    IEnumerator AutoStopVoice()
    {
        yield return new WaitForSeconds(MAX_RECORD_SECS);
        if (_isRecording) StopVoice();
    }

    void StopVoice()
    {
        if (!_isRecording) return;
        _isRecording = false;
        int last = Microphone.GetPosition(null);
        Microphone.End(null);
        SetVoiceColor(false);
        if (_recordedClip == null || last <= 0)
        {
            StartCoroutine(SpawnBotBubble("I couldn't hear anything. Please try again.", 0f));
            return;
        }
        float[] samples = new float[last * _recordedClip.channels];
        _recordedClip.GetData(samples, 0);
        byte[] wav = ConvertToWav(samples, _recordedClip.channels, SAMPLE_RATE);
        ShowTyping(true);
        StartCoroutine(AskGeminiAudio(wav));
    }

    void SetVoiceColor(bool recording)
    {
        if (_voiceBtn == null) return;
        var img = _voiceBtn.GetComponent<Image>();
        if (img) img.color = recording ? new Color(0.85f, 0.15f, 0.15f, 1f) : Hex("1A1535");
    }

    // ════════════════════════════════════════════════════════════
    //  GEMINI — TEXT
    // ════════════════════════════════════════════════════════════
    IEnumerator AskGemini(string question)
    {
        // Use system_instruction (separate from user turn) so the model
        // doesn't confuse the system prompt with the student's message.
        // Hand-build JSON but escape each field individually for safety.
        string body =
            "{" +
              "\"system_instruction\":{\"parts\":[{\"text\":\"" + EscapeJson(SYSTEM_PROMPT) + "\"}]}," +
              "\"contents\":[{\"role\":\"user\",\"parts\":[{\"text\":\"" + EscapeJson(question) + "\"}]}]," +
              "\"generationConfig\":{\"maxOutputTokens\":300,\"temperature\":0.7}" +
            "}";
        Debug.Log("[Gemini] Sending text request...");
        yield return StartCoroutine(SendToGemini(Encoding.UTF8.GetBytes(body)));
    }

    // ════════════════════════════════════════════════════════════
    //  GEMINI — AUDIO
    // ════════════════════════════════════════════════════════════
    IEnumerator AskGeminiAudio(byte[] wav)
    {
        string b64 = Convert.ToBase64String(wav);
        string audioInstruction =
            "The student asked a question via voice recording. " +
            "Transcribe what they said and answer accordingly.";
        string body =
            "{" +
              "\"system_instruction\":{\"parts\":[{\"text\":\"" + EscapeJson(SYSTEM_PROMPT) + "\"}]}," +
              "\"contents\":[{\"role\":\"user\",\"parts\":[" +
                "{\"inline_data\":{\"mime_type\":\"audio/wav\",\"data\":\"" + b64 + "\"}}," +
                "{\"text\":\"" + EscapeJson(audioInstruction) + "\"}" +
              "]}]," +
              "\"generationConfig\":{\"maxOutputTokens\":300,\"temperature\":0.7}" +
            "}";
        Debug.Log("[Gemini] Sending audio request...");
        yield return StartCoroutine(SendToGemini(Encoding.UTF8.GetBytes(body)));
    }

    // ════════════════════════════════════════════════════════════
    //  SEND TO GEMINI
    // ════════════════════════════════════════════════════════════
    IEnumerator SendToGemini(byte[] body)
    {
        // Guard: bail early if the key hasn't been set
        if (string.IsNullOrEmpty(apiKey) || apiKey == "PASTE_YOUR_GEMINI_API_KEY_HERE")
        {
            ShowTyping(false);
            _isRequestRunning     = false;
            _sendBtn.interactable = true;
            SetSuggestionsInteractable(true);
            if (_chatScreen != null && _chatScreen.activeSelf)
                StartCoroutine(SpawnBotBubble(
                    "No API key set. Paste your Gemini API key into the ChatbotUIBuilder component in the Inspector.", 0f));
            yield break;
        }

        string url = _apiUrl + "?key=" + apiKey;
        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        Debug.Log($"[Gemini] HTTP {req.responseCode} | result={req.result}");
        ShowTyping(false);

        if (req.responseCode == 403)
        {
            Debug.LogError("[Gemini] 403 Forbidden — API key is invalid, expired, or Gemini API is not enabled for this key. Visit https://aistudio.google.com/app/apikey");
            _isRequestRunning     = false;
            _sendBtn.interactable = true;
            SetSuggestionsInteractable(true);
            if (_chatScreen != null && _chatScreen.activeSelf)
                StartCoroutine(SpawnBotBubble(
                    "API key error (403). Please paste a valid Gemini API key into the ChatbotUIBuilder component in Inspector.", 0f));
        }
        else if (req.responseCode == 429)
        {
            _requestCooldown = 30f;
            _lastRequestTime = Time.time;
            _isRequestRunning = false;
            if (_countdownCo != null) StopCoroutine(_countdownCo);
            _countdownCo = StartCoroutine(Countdown(30f));
            if (_chatScreen != null && _chatScreen.activeSelf)
                StartCoroutine(SpawnBotBubble("Too many requests! Please wait 30 seconds.", 0f));
        }
        else if (req.result == UnityWebRequest.Result.Success)
        {
            string answer = ParseResponse(req.downloadHandler.text);
            Debug.Log("[Gemini] Parsed answer (" + answer.Length + " chars): "
                + answer.Substring(0, Mathf.Min(80, answer.Length)));
            Debug.Log("[Gemini] chatScreen.active=" + (_chatScreen != null ? _chatScreen.activeSelf.ToString() : "NULL")
                + "  contentArea=" + (_contentArea == null ? "NULL" : "OK")
                + "  botPrefab=" + (_botBubblePrefab == null ? "NULL" : "OK"));

            _lastAnswer           = answer;
            _isRequestRunning     = false;
            _requestCooldown      = 5f;
            _sendBtn.interactable = true;
            SetSuggestionsInteractable(true);

            // Ensure chat screen is visible — it may have been hidden while the request was in flight
            if (_chatScreen != null && !_chatScreen.activeSelf)
            {
                Debug.LogWarning("[Gemini] Chat screen was inactive when response arrived — re-activating.");
                _chatScreen.SetActive(true);
            }

            if (_contentArea != null && _botBubblePrefab != null)
            {
                StartCoroutine(SpawnBotBubble(answer, 0f));
                StartCoroutine(Speak(answer));
            }
            else
            {
                Debug.LogError("[Gemini] Cannot spawn bubble: contentArea="
                    + (_contentArea == null ? "NULL" : "OK")
                    + " botPrefab=" + (_botBubblePrefab == null ? "NULL" : "OK"));
            }
        }
        else
        {
            Debug.LogError("[Gemini] Request failed: " + req.error + " | HTTP " + req.responseCode);
            _isRequestRunning     = false;
            _sendBtn.interactable = true;
            SetSuggestionsInteractable(true);
            if (_chatScreen != null && _chatScreen.activeSelf)
                StartCoroutine(SpawnBotBubble("Connection failed. Check your internet and try again.", 0f));
        }
    }

    // ── Gemini response structs for JsonUtility ──────────────────
    [Serializable] class GeminiResponse   { public GeminiCandidate[] candidates; }
    [Serializable] class GeminiCandidate  { public GeminiContent content; }
    [Serializable] class GeminiContent    { public GeminiPart[] parts; }
    [Serializable] class GeminiPart       { public string text; }

    string ParseResponse(string json)
    {
        // Always log the raw response so you can see exactly what Gemini returned
        Debug.Log("[Gemini RAW] " + json);
        try
        {
            var root = JsonUtility.FromJson<GeminiResponse>(json);
            if (root == null)
            {
                Debug.LogWarning("[Gemini] JsonUtility returned null — trying fallback parse");
                return FallbackParse(json);
            }
            if (root.candidates == null || root.candidates.Length == 0)
            {
                Debug.LogWarning("[Gemini] No candidates in response. Full JSON logged above.");
                return "I couldn't generate a response. Please try again.";
            }
            var parts = root.candidates[0]?.content?.parts;
            if (parts == null || parts.Length == 0)
            {
                Debug.LogWarning("[Gemini] Candidate has no parts.");
                return "I couldn't generate a response. Please try again.";
            }
            string text = parts[0].text;
            if (string.IsNullOrEmpty(text))
            {
                Debug.LogWarning("[Gemini] Part text is empty.");
                return "I received an empty response. Please try again.";
            }
            return text.Trim();
        }
        catch (Exception ex)
        {
            Debug.LogError("[Gemini] ParseResponse exception: " + ex.Message);
            return FallbackParse(json);
        }
    }

    // Fallback: scan for the LAST "text": field to skip safetyRatings / prompt echo
    string FallbackParse(string json)
    {
        try
        {
            // Walk backwards from the end to find the last "text": occurrence
            int i = json.LastIndexOf("\"text\":");
            if (i < 0) return "No response received.";
            // skip:  "text":_"
            int s = json.IndexOf('"', i + 7);
            if (s < 0) return "No response received.";
            s++; // move past the opening quote
            // Walk char-by-char to handle escaped quotes correctly
            var sb = new StringBuilder();
            int pos = s;
            while (pos < json.Length)
            {
                char c = json[pos];
                if (c == '\\' && pos + 1 < json.Length)
                {
                    char next = json[pos + 1];
                    switch (next)
                    {
                        case 'n':  sb.Append('\n'); pos += 2; continue;
                        case 'r':  sb.Append('\r'); pos += 2; continue;
                        case 't':  sb.Append('\t'); pos += 2; continue;
                        case '"':  sb.Append('"');  pos += 2; continue;
                        case '\\': sb.Append('\\'); pos += 2; continue;
                        default:   sb.Append(next); pos += 2; continue;
                    }
                }
                if (c == '"') break; // end of string value
                sb.Append(c);
                pos++;
            }
            string result = sb.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "Empty response from Gemini." : result;
        }
        catch (Exception ex)
        {
            Debug.LogError("[Gemini] FallbackParse exception: " + ex.Message);
            return "Could not read response.";
        }
    }

    // ════════════════════════════════════════════════════════════
    //  BUBBLE SPAWNING
    // ════════════════════════════════════════════════════════════
    void SpawnUserBubble(string text)
    {
        if (_userBubblePrefab == null) { Debug.LogError("[Bubble] _userBubblePrefab is null"); return; }
        if (_contentArea == null) { Debug.LogError("[Bubble] _contentArea is null"); return; }
        var go = Instantiate(_userBubblePrefab, _contentArea);
        go.SetActive(true);

        var tmp = FindMessageTextTMP(go.transform);
        if (tmp == null)
        {
            Debug.LogError("[Bubble] Could not find MessageText TMP in user bubble hierarchy!");
            return;
        }

        tmp.text = text;
        tmp.ForceMeshUpdate();
        LayoutRebuilder.ForceRebuildLayoutImmediate(tmp.rectTransform);

        NormalizeInstantiatedBubble(go);

        EnsureBubbleLayoutVisible(go);
        Canvas.ForceUpdateCanvases();

        LayoutRebuilder.ForceRebuildLayoutImmediate(go.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentArea);
        StartCoroutine(FadeIn(go, 0f));
        StartCoroutine(ScrollBottom());
    }

    IEnumerator SpawnBotBubble(string text, float delay)
    {
        // Wait at least one frame so layout is ready; skip long delays that
        // allow the content area to be destroyed between check and spawn.
        if (delay > 0f) yield return new WaitForSeconds(delay);
        else            yield return null;   // one frame only

        if (_contentArea == null) { Debug.LogError("[Bubble] _contentArea is null after delay"); yield break; }
        if (_botBubblePrefab == null) { Debug.LogError("[Bubble] _botBubblePrefab is null after delay"); yield break; }

        var go = Instantiate(_botBubblePrefab, _contentArea);
        go.SetActive(true);

        var tmp = FindMessageTextTMP(go.transform);
        if (tmp == null)
        {
            Debug.LogError("[Bubble] FAILED to set text. Hierarchy: "
                + GetHierarchyString(go.transform));
            yield break;
        }

        tmp.text = text;
        tmp.ForceMeshUpdate();
        LayoutRebuilder.ForceRebuildLayoutImmediate(tmp.rectTransform);

        NormalizeInstantiatedBubble(go);

        EnsureBubbleLayoutVisible(go);
        // Force immediate layout rebuild so the bubble has correct height
        // before FadeIn reads it — otherwise all bubbles collapse to height 0
        Canvas.ForceUpdateCanvases();

        LayoutRebuilder.ForceRebuildLayoutImmediate(go.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentArea);
        StartCoroutine(FadeIn(go, 0f));
        StartCoroutine(ScrollBottom());
    }

    void NormalizeInstantiatedBubble(GameObject bubbleRowRoot)
    {
        if (bubbleRowRoot == null) return;

        var rootRT = bubbleRowRoot.GetComponent<RectTransform>();
        if (rootRT != null)
        {
            rootRT.localScale = Vector3.one;
            rootRT.localRotation = Quaternion.identity;
            rootRT.localPosition = Vector3.zero;
            // LayoutGroup will reposition anyway, but a clean anchored pos prevents accidental offscreen values.
            rootRT.anchoredPosition = Vector2.zero;
        }

        // Avoid weird scaling coming from prefab authoring.
        foreach (var tr in bubbleRowRoot.GetComponentsInChildren<Transform>(true))
            tr.localScale = Vector3.one;

        // Debug visibility check
        var tmp = FindMessageTextTMP(bubbleRowRoot.transform);
        if (tmp != null)
        {
            Debug.Log("[Bubble] text assigned OK len=" + tmp.text.Length +
                      " activeSelf=" + tmp.gameObject.activeSelf +
                      " colorA=" + tmp.color.a);
        }
    }

    void EnsureBubbleLayoutVisible(GameObject rowRoot)
    {
        if (rowRoot == null) return;

        var tmp = FindMessageTextTMP(rowRoot.transform);
        if (tmp == null) return;

        tmp.ForceMeshUpdate();

        // Ensure text gets measurable width for wrapping, then assign measured height.
        var msgRT = tmp.rectTransform;
        float width = msgRT.sizeDelta.x > 1f ? msgRT.sizeDelta.x : SU(360);
        float prefH = Mathf.Max(SU(36), tmp.GetPreferredValues(tmp.text, width, 10000f).y);
        msgRT.sizeDelta = new Vector2(width, prefH);

        var msgLE = tmp.GetComponent<LayoutElement>() ?? tmp.gameObject.AddComponent<LayoutElement>();
        msgLE.preferredWidth = width;
        msgLE.preferredHeight = prefH;
        msgLE.minHeight = prefH;
        msgLE.flexibleHeight = 0;

        // Bubble root should at least fit text + top/bottom padding.
        var bubbleTf = FindBubbleTransform(tmp.transform, rowRoot.transform);
        if (bubbleTf != null)
        {
            var bubbleLE = bubbleTf.GetComponent<LayoutElement>() ?? bubbleTf.gameObject.AddComponent<LayoutElement>();
            float bubbleH = prefH + SU(28);
            bubbleLE.preferredHeight = bubbleH;
            bubbleLE.minHeight = bubbleH;
            bubbleLE.flexibleHeight = 0;

            var bubbleRT = bubbleTf.GetComponent<RectTransform>();
            if (bubbleRT != null)
                bubbleRT.sizeDelta = new Vector2(bubbleRT.sizeDelta.x, bubbleH);
        }

        // Row root should not collapse.
        var rowLE = rowRoot.GetComponent<LayoutElement>() ?? rowRoot.AddComponent<LayoutElement>();
        float rowH = prefH + SU(34);
        rowLE.preferredHeight = rowH;
        rowLE.minHeight = rowH;
        rowLE.flexibleHeight = 0;

        var rowRT = rowRoot.GetComponent<RectTransform>();
        if (rowRT != null)
            rowRT.sizeDelta = new Vector2(rowRT.sizeDelta.x, rowH);
    }

    TextMeshProUGUI FindMessageTextTMP(Transform root)
    {
        if (root == null) return null;

        var direct = root.Find("Bubble/MessageText");
        if (direct != null)
        {
            var directTmp = direct.GetComponent<TextMeshProUGUI>();
            if (directTmp != null) return directTmp;
        }

        foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (t != null && t.gameObject.name == "MessageText")
                return t;

        // Last fallback: first TMP found in this hierarchy.
        return root.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    Transform FindBubbleTransform(Transform from, Transform rowRoot)
    {
        var cur = from;
        while (cur != null && cur != rowRoot)
        {
            if (cur.name == "Bubble") return cur;
            cur = cur.parent;
        }

        var direct = rowRoot.Find("Bubble");
        if (direct != null) return direct;

        foreach (var t in rowRoot.GetComponentsInChildren<Transform>(true))
            if (t != null && t.name == "Bubble")
                return t;

        return null;
    }

    // Debug helper — prints full child hierarchy of a transform
    static string GetHierarchyString(Transform t, string indent = "")
    {
        var sb = new StringBuilder();
        sb.AppendLine(indent + t.name + " [" + t.GetComponents<Component>().Length + " components]");
        foreach (Transform child in t)
            sb.Append(GetHierarchyString(child, indent + "  "));
        return sb.ToString();
    }

    IEnumerator SpawnMetaLine(string text, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_contentArea == null) yield break;

        // MakeGO guarantees RectTransform exists; pre-add CanvasGroup for FadeIn
        var go  = MakeGO("MetaLine", _contentArea);
        go.AddComponent<CanvasGroup>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = SU(14);
        tmp.color     = Hex("9F7AEA");
        tmp.fontStyle = FontStyles.Normal;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(SU(640), SU(24));
        StartCoroutine(FadeIn(go, 0f));
    }

    // ════════════════════════════════════════════════════════════
    //  SUGGESTION CHIPS
    // ════════════════════════════════════════════════════════════
    IEnumerator SpawnSuggestionChip(string text, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_contentArea == null) yield break;

        // ── outer container (centres the chip) ──────────────────
        // MakeGO guarantees RectTransform exists; pre-add CanvasGroup for FadeIn
        var container = MakeGO("SuggestionContainer", _contentArea);
        container.AddComponent<CanvasGroup>();
        var cRt = container.GetComponent<RectTransform>();
        cRt.sizeDelta = Vector2.zero;
        var hlg = container.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment        = TextAnchor.MiddleCenter;
        hlg.childControlWidth     = false;
        hlg.childControlHeight    = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        var cCsf = container.AddComponent<ContentSizeFitter>();
        cCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        cCsf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // ── chip GO: Image + Button only ────────────────────────
        var chipGO = new GameObject("SuggestionChip");
        chipGO.transform.SetParent(container.transform, false);
        var chipRt = chipGO.AddComponent<RectTransform>();
        chipRt.sizeDelta = new Vector2(SU(640), SU(80));
        chipRt.pivot     = new Vector2(0.5f, 0.5f);

        var img = chipGO.AddComponent<Image>();
        img.color = Hex("7C3AED");
        ApplyRounded(img);

        var btn = chipGO.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable  = true;

        string capturedText = text;
        btn.onClick.AddListener(() => OnSuggestionClicked(capturedText));

        // ── text lives in a CHILD of the chip ───────────────────
        var labelGO  = new GameObject("Label");
        labelGO.transform.SetParent(chipGO.transform, false);
        var labelRt  = labelGO.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(SU(14), SU(8));
        labelRt.offsetMax = new Vector2(-SU(14), -SU(8));

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text            = text;
        tmp.fontSize        = SU(18);
        tmp.color           = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.alignment       = TextAlignmentOptions.Center;
        tmp.overflowMode    = TextOverflowModes.Ellipsis;
        tmp.raycastTarget   = false;

        _suggestionButtons.Add(btn);
        StartCoroutine(FadeIn(container, 0f));
        StartCoroutine(ScrollBottom());
    }

    void OnSuggestionClicked(string text)
    {
        if (_isRequestRunning) return;
        if (_inputField == null) return;
        if (string.IsNullOrWhiteSpace(text)) return;
        if (_isRecording) { Microphone.End(null); _isRecording = false; SetVoiceColor(false); }
        _inputField.text = text;
        OnSendClicked();
    }

    void SetSuggestionsInteractable(bool enabled)
    {
        for (int i = 0; i < _suggestionButtons.Count; i++)
        {
            var b = _suggestionButtons[i];
            if (b != null) b.interactable = enabled;
        }
    }

    void ClearChat()
    {
        _suggestionButtons.Clear();
        if (_contentArea == null) return;
        foreach (Transform child in _contentArea)
            Destroy(child.gameObject);
    }

    // ════════════════════════════════════════════════════════════
    //  ANIMATIONS
    // ════════════════════════════════════════════════════════════
    IEnumerator FadeIn(GameObject go, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (go == null) yield break;

        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();

        // Start invisible — let the layout system position it first
        cg.alpha = 0f;
        Canvas.ForceUpdateCanvases();

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            go.GetComponent<RectTransform>());

        if (_contentArea != null)
        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentArea);

        yield return null;

        

        if (go == null) yield break;

        // Force layout rebuild so preferred sizes are up to date
        LayoutRebuilder.ForceRebuildLayoutImmediate(
            go.GetComponent<RectTransform>());
        if (_contentArea != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentArea);

        // Fade in only — no position animation (layout controls position)
        float dur = 0.18f, t = 0f;
        while (t < dur)
        {
            if (go == null) yield break;
            t += Time.deltaTime;
            cg.alpha = Mathf.SmoothStep(0f, 1f, t / dur);
            yield return null;
        }
        if (go != null) cg.alpha = 1f;
        StartCoroutine(ScrollBottom());
    }

    IEnumerator ScrollBottom()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        if (_scrollRect == null) yield break;
        float from = _scrollRect.verticalNormalizedPosition;
        float dur = 0.25f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            _scrollRect.verticalNormalizedPosition =
                Mathf.Lerp(from, 0f, Mathf.SmoothStep(0, 1, t / dur));
            yield return null;
        }
        _scrollRect.verticalNormalizedPosition = 0f;
    }

    void ShowTyping(bool show)
    {
        if (_typingIndicator != null) _typingIndicator.SetActive(show);
        if (show) StartCoroutine(ScrollBottom());
    }

    // ════════════════════════════════════════════════════════════
    //  TTS
    // ════════════════════════════════════════════════════════════
    IEnumerator Speak(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        string url = "https://translate.google.com/translate_tts?ie=UTF-8&q=" +
                     UnityWebRequest.EscapeURL(text) + "&tl=en&client=tw-ob";
        using var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success && _audioSource != null)
        {
            _audioSource.clip = DownloadHandlerAudioClip.GetContent(req);
            _audioSource.Play();
        }
    }

    // ════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════
    GameObject MakeGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    TextMeshProUGUI MakeTMP(Transform parent, string text, float size, Color color, bool bold)
    {
        var go  = new GameObject(text.Length > 12 ? "Label" : text);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        return tmp;
    }

    GameObject CreatePanel(Transform parent, string name, Color color,
                            Vector2 anchorMin, Vector2 anchorMax,
                            Vector2 offsetMin, Vector2 offsetMax)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        ApplyRounded(img);
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        return go;
    }

    void StretchFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static Color Hex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6) hex += "FF";
        return new Color(
            Convert.ToInt32(hex.Substring(0, 2), 16) / 255f,
            Convert.ToInt32(hex.Substring(2, 2), 16) / 255f,
            Convert.ToInt32(hex.Substring(4, 2), 16) / 255f,
            Convert.ToInt32(hex.Substring(6, 2), 16) / 255f);
    }

    static string EscapeJson(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"")
         .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    byte[] ConvertToWav(float[] samples, int channels, int sampleRate)
    {
        int bc = samples.Length * 2, total = 44 + bc;
        byte[] wav = new byte[total];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        BitConverter.GetBytes(total - 8).CopyTo(wav, 4);
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
        BitConverter.GetBytes(bc).CopyTo(wav, 40);
        int off = 44;
        foreach (float s in samples)
        {
            short v = (short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue);
            BitConverter.GetBytes(v).CopyTo(wav, off); off += 2;
        }
        return wav;
    }
}

// ════════════════════════════════════════════════════════════════
//  TYPING DOTS ANIMATION
// ════════════════════════════════════════════════════════════════
public class TypingDotsAnim : MonoBehaviour
{
    private Image _d0, _d1, _d2;
    private float _t;

    public void Init(Image d0, Image d1, Image d2)
    { _d0 = d0; _d1 = d1; _d2 = d2; }

    void Update()
    {
        if (_d0 == null) return;
        _t += Time.deltaTime * 3f;
        Color c = new Color(0.49f, 0.23f, 0.93f, 1f);
        _d0.color = new Color(c.r, c.g, c.b, Mathf.Abs(Mathf.Sin(_t)));
        _d1.color = new Color(c.r, c.g, c.b, Mathf.Abs(Mathf.Sin(_t + 0.6f)));
        _d2.color = new Color(c.r, c.g, c.b, Mathf.Abs(Mathf.Sin(_t + 1.2f)));
    }
}

// Compatibility shim — scenes serialized with AIDoubtSolver keep working
public class AIDoubtSolver : ChatbotUIBuilder { }