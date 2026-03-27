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

    [Header("Gemini API")]
    [Tooltip("Paste your Google AI Studio API key here.")]
    public string apiKey = "AIzaSyDlGl10FOHUfNAiI_ICCJlDYRSzqXF0wno";

    [Header("UI Readability")]
    [Range(1.0f, 2.5f)]
    public float uiScale = 1.6f;

    // ── runtime refs ──────────────────────────────────────────
    private GameObject     _chatScreen;
    private ScrollRect     _scrollRect;
    private RectTransform  _contentArea;
    private TMP_InputField _inputField;
    private Button         _sendBtn;
    private Button         _voiceBtn;
    private GameObject     _typingIndicator;
    private AudioSource    _audioSource;
    private TextMeshProUGUI _badgeText;
    private Image           _badgeBg;

    // ── state ─────────────────────────────────────────────────
    private string    _lastAnswer           = "";
    private bool      _isRecording          = false;
    private AudioClip _recordedClip         = null;
    private bool      _isRequestRunning     = false;
    private float     _lastRequestTime      = -999f;
    private float     _requestCooldown      = 5f;
    private Coroutine _countdownCo          = null;
    private bool      _openedByAttention    = false;
    private string    _lastAttentionState   = "";
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

    // ═══════════════════════════════════════════════════════════
    void Start()
    {
        _audioSource = gameObject.GetComponent<AudioSource>()
                    ?? gameObject.AddComponent<AudioSource>();
        BuildUI();
        if (askAIButton != null)
            askAIButton.onClick.AddListener(OpenChatbot);
        ShowTutorial();
    }

    // ═══════════════════════════════════════════════════════════
    //  SPRITE HELPER
    // ═══════════════════════════════════════════════════════════
    static Sprite _roundedSprite;
    static Sprite GetRoundedSprite()
    {
        if (_roundedSprite != null) return _roundedSprite;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px  = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        _roundedSprite = Sprite.Create(tex, new Rect(0,0,4,4), new Vector2(0.5f,0.5f),
            1f, 1u, SpriteMeshType.FullRect, new Vector4(1,1,1,1));
        return _roundedSprite;
    }
    static void ApplyRounded(Image img)
    {
        if (img == null) return;
        img.sprite = GetRoundedSprite();
        img.type   = Image.Type.Sliced;
    }

    float SU(float v) => v * uiScale;
    int   SI(int   v) => Mathf.RoundToInt(v * uiScale);

    // ═══════════════════════════════════════════════════════════
    //  BUILD UI
    // ═══════════════════════════════════════════════════════════
    void BuildUI()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("[ChatbotUI] No Canvas found!"); return; }

        Transform old = canvas.transform.Find("ChatbotScreen");
        if (old != null) Destroy(old.gameObject);

        _chatScreen = CreatePanel(canvas.transform, "ChatbotScreen",
            Hex("0A0A14"), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        _chatScreen.SetActive(false);

        BuildHeader();
        BuildDivider(_chatScreen.transform, -SU(64), -SU(63));
        BuildScrollView();
        BuildTypingIndicator();
        BuildInputBar();
    }

    // ───────────────────────────────────────────────────────────
    //  HEADER
    // ───────────────────────────────────────────────────────────
    void BuildHeader()
    {
        var header = CreatePanel(_chatScreen.transform, "Header",
            Hex("100B28"),
            new Vector2(0,1), new Vector2(1,1),
            new Vector2(0,-SU(92)), new Vector2(0,0));

        var av    = MakeGO("Avatar", header.transform);
        var avImg = av.AddComponent<Image>(); avImg.color = Hex("7C3AED");
        var avRT  = av.GetComponent<RectTransform>();
        avRT.anchorMin = new Vector2(0,0.5f); avRT.anchorMax = new Vector2(0,0.5f);
        avRT.pivot = new Vector2(0,0.5f);
        avRT.anchoredPosition = new Vector2(SU(18),0);
        avRT.sizeDelta = new Vector2(SU(52),SU(52));
        var avTxt = MakeTMP(av.transform,"AI",SU(18),Color.white,true);
        StretchFill(avTxt.GetComponent<RectTransform>());
        avTxt.alignment = TextAlignmentOptions.Center;

        var title = MakeTMP(header.transform,"AI Teacher",SU(22),Hex("E2D9F3"),true);
        var tRT   = title.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0,0.5f); tRT.anchorMax = new Vector2(0,0.5f);
        tRT.pivot = new Vector2(0,0.5f);
        tRT.anchoredPosition = new Vector2(SU(84),SU(14));
        tRT.sizeDelta = new Vector2(SU(360),SU(30));

        var sub = MakeTMP(header.transform,"ML Learning Assistant",SU(14),Hex("9F7AEA"),false);
        var sRT = sub.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0,0.5f); sRT.anchorMax = new Vector2(0,0.5f);
        sRT.pivot = new Vector2(0,0.5f);
        sRT.anchoredPosition = new Vector2(SU(84),-SU(14));
        sRT.sizeDelta = new Vector2(SU(380),SU(24));

        var badge = MakeGO("Badge", header.transform);
        _badgeBg  = badge.AddComponent<Image>();
        _badgeBg.color = new Color(0.08f,0.35f,0.12f,0.85f);
        var bRT = badge.GetComponent<RectTransform>();
        bRT.anchorMin = new Vector2(1,0.5f); bRT.anchorMax = new Vector2(1,0.5f);
        bRT.pivot = new Vector2(1,0.5f);
        bRT.anchoredPosition = new Vector2(-SU(74),0);
        bRT.sizeDelta = new Vector2(SU(140),SU(34));
        _badgeText = MakeTMP(badge.transform,"● FOCUSED",SU(13),Hex("4ADE80"),true);
        StretchFill(_badgeText.GetComponent<RectTransform>());
        _badgeText.alignment = TextAlignmentOptions.Center;

        var closeGO  = MakeGO("CloseBtn", header.transform);
        var closeImg = closeGO.AddComponent<Image>(); closeImg.color = Hex("1A1535");
        var cRT = closeGO.GetComponent<RectTransform>();
        cRT.anchorMin = new Vector2(1,0.5f); cRT.anchorMax = new Vector2(1,0.5f);
        cRT.pivot = new Vector2(1,0.5f);
        cRT.anchoredPosition = new Vector2(-SU(14),0);
        cRT.sizeDelta = new Vector2(SU(40),SU(40));
        var closeBtn = closeGO.AddComponent<Button>();
        var cTxt = MakeTMP(closeGO.transform,"X",SU(20),Hex("9F7AEA"),true);
        StretchFill(cTxt.GetComponent<RectTransform>());
        cTxt.alignment = TextAlignmentOptions.Center;
        closeBtn.onClick.AddListener(CloseChatbot);
    }

    // ───────────────────────────────────────────────────────────
    //  DIVIDER
    // ───────────────────────────────────────────────────────────
    void BuildDivider(Transform parent, float top, float bottom)
    {
        var d   = MakeGO("Divider", parent);
        var img = d.AddComponent<Image>(); img.color = Hex("2D1F5E");
        var rt  = d.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0,1); rt.anchorMax = new Vector2(1,1);
        rt.offsetMin = new Vector2(0,top);
        rt.offsetMax = new Vector2(0,bottom);
    }

    // ───────────────────────────────────────────────────────────
    //  SCROLL VIEW
    //  ★ FIX: childForceExpandHeight = FALSE  (was TRUE — crushed all heights to 0)
    //  ★ FIX: childControlHeight     = FALSE  (let each bubble row own its height)
    // ───────────────────────────────────────────────────────────
    void BuildScrollView()
    {
        var svGO  = MakeGO("ScrollView", _chatScreen.transform);
        var svImg = svGO.AddComponent<Image>(); svImg.color = Hex("0A0A14");
        var svRT  = svGO.GetComponent<RectTransform>();
        svRT.anchorMin = new Vector2(0,0);
        svRT.anchorMax = new Vector2(1,1);
        svRT.offsetMin = new Vector2(0, SU(108));
        svRT.offsetMax = new Vector2(0,-SU(92));

        var sr = svGO.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Elastic;
        sr.elasticity = 0.1f; sr.inertia = true; sr.decelerationRate = 0.135f;
        _scrollRect = sr;

        var vpGO  = MakeGO("Viewport", svGO.transform);
        var vpImg = vpGO.AddComponent<Image>(); vpImg.color = Color.clear;
        vpGO.AddComponent<RectMask2D>();
        var vpRT  = vpGO.GetComponent<RectTransform>();
        StretchFill(vpRT);
        sr.viewport = vpRT;

        var cGO = MakeGO("ContentArea", vpGO.transform);
        _contentArea = cGO.GetComponent<RectTransform>();
        _contentArea.anchorMin        = new Vector2(0,1);
        _contentArea.anchorMax        = new Vector2(1,1);
        _contentArea.pivot            = new Vector2(0.5f,1);
        _contentArea.anchoredPosition = Vector2.zero;
        _contentArea.sizeDelta        = Vector2.zero;

        var vlg = cGO.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing        = SU(14);
        vlg.padding        = new RectOffset(SI(18),SI(18),SI(20),SI(20));
        // Let layout group size each row from its LayoutElement settings.
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = cGO.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = _contentArea;
    }

    // ───────────────────────────────────────────────────────────
    //  TYPING INDICATOR
    // ───────────────────────────────────────────────────────────
    void BuildTypingIndicator()
    {
        _typingIndicator = MakeGO("TypingIndicator", _chatScreen.transform);
        var rt = _typingIndicator.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0,0); rt.anchorMax = new Vector2(1,0);
        rt.pivot = new Vector2(0.5f,0);
        rt.anchoredPosition = new Vector2(0,SU(108));
        rt.sizeDelta = new Vector2(0,SU(54));

        var hlg = _typingIndicator.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft; hlg.spacing = SU(10);
        hlg.padding = new RectOffset(SI(20),SI(20),SI(8),SI(8));
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        var dot    = MakeGO("Dot",_typingIndicator.transform);
        dot.AddComponent<Image>().color = Hex("7C3AED");
        dot.GetComponent<RectTransform>().sizeDelta = new Vector2(SU(34),SU(34));
        var dl = MakeTMP(dot.transform,"AI",SU(11),Color.white,true);
        StretchFill(dl.GetComponent<RectTransform>());
        dl.alignment = TextAlignmentOptions.Center;

        var bub    = MakeGO("Bubble",_typingIndicator.transform);
        var bubImg = bub.AddComponent<Image>(); bubImg.color = Hex("1A1040");
        ApplyRounded(bubImg);
        bub.GetComponent<RectTransform>().sizeDelta = new Vector2(SU(88),SU(42));
        var bhlg = bub.AddComponent<HorizontalLayoutGroup>();
        bhlg.childAlignment = TextAnchor.MiddleCenter; bhlg.spacing = 5;
        bhlg.padding = new RectOffset(10,10,8,8);
        bhlg.childControlWidth = false; bhlg.childControlHeight = false;
        bhlg.childForceExpandWidth = false; bhlg.childForceExpandHeight = false;

        var dots = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var d = MakeGO("D"+i, bub.transform);
            dots[i] = d.AddComponent<Image>(); dots[i].color = Hex("7C3AED");
            d.GetComponent<RectTransform>().sizeDelta = new Vector2(7,7);
        }
        _typingIndicator.AddComponent<TypingDotsAnim>().Init(dots[0],dots[1],dots[2]);
        _typingIndicator.SetActive(false);
    }

    // ───────────────────────────────────────────────────────────
    //  INPUT BAR
    // ───────────────────────────────────────────────────────────
    void BuildInputBar()
    {
        var bar = CreatePanel(_chatScreen.transform,"InputBar",
            Hex("100B28"),
            new Vector2(0,0), new Vector2(1,0),
            new Vector2(0,0), new Vector2(0,SU(100)));

        var div = MakeGO("Div",bar.transform);
        div.AddComponent<Image>().color = Hex("2D1F5E");
        var dRT = div.GetComponent<RectTransform>();
        dRT.anchorMin = new Vector2(0,1); dRT.anchorMax = new Vector2(1,1);
        dRT.pivot = new Vector2(0.5f,1); dRT.anchoredPosition = Vector2.zero;
        dRT.sizeDelta = new Vector2(0,1);

        var bg    = MakeGO("InputBg",bar.transform);
        var bgImg = bg.AddComponent<Image>(); bgImg.color = Hex("1A1040");
        ApplyRounded(bgImg);
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0,0.5f); bgRT.anchorMax = new Vector2(1,0.5f);
        bgRT.pivot = new Vector2(0.5f,0.5f);
        bgRT.offsetMin = new Vector2(SU(18),-SU(30));
        bgRT.offsetMax = new Vector2(-SU(150),SU(30));

        var ifGO  = MakeGO("InputField",bar.transform);
        ifGO.AddComponent<Image>().color = Color.clear;
        var ifRT  = ifGO.GetComponent<RectTransform>();
        ifRT.anchorMin = new Vector2(0,0.5f); ifRT.anchorMax = new Vector2(1,0.5f);
        ifRT.pivot = new Vector2(0.5f,0.5f);
        ifRT.offsetMin = new Vector2(SU(18),-SU(30));
        ifRT.offsetMax = new Vector2(-SU(150),SU(30));

        var ta   = MakeGO("TextArea",ifGO.transform);
        var taRT = ta.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(SU(16),SU(6));
        taRT.offsetMax = new Vector2(-SU(16),-SU(6));
        ta.AddComponent<RectMask2D>();

        var ph  = MakeGO("Placeholder",ta.transform);
        var phT = ph.AddComponent<TextMeshProUGUI>();
        phT.text = "Ask me anything about ML..."; phT.fontSize = SU(19);
        phT.color = Hex("4A3F7B"); phT.fontStyle = FontStyles.Italic;
        StretchFill(ph.GetComponent<RectTransform>());

        var tx  = MakeGO("Text",ta.transform);
        var txT = tx.AddComponent<TextMeshProUGUI>();
        txT.fontSize = SU(19); txT.color = Hex("E2D9F3");
        StretchFill(tx.GetComponent<RectTransform>());

        _inputField = ifGO.AddComponent<TMP_InputField>();
        _inputField.textViewport  = taRT;
        _inputField.textComponent = txT;
        _inputField.placeholder   = phT;
        _inputField.caretColor    = Hex("9F7AEA");
        _inputField.selectionColor = new Color(0.49f,0.23f,0.93f,0.35f);
        _inputField.onSubmit.AddListener((_) => OnSendClicked());

        var vGO  = MakeGO("VoiceBtn",bar.transform);
        var vImg = vGO.AddComponent<Image>(); vImg.color = Hex("1A1535");
        ApplyRounded(vImg);
        var vRT  = vGO.GetComponent<RectTransform>();
        vRT.anchorMin = new Vector2(1,0.5f); vRT.anchorMax = new Vector2(1,0.5f);
        vRT.pivot = new Vector2(1,0.5f);
        vRT.anchoredPosition = new Vector2(-SU(82),0); vRT.sizeDelta = new Vector2(SU(62),SU(62));
        _voiceBtn = vGO.AddComponent<Button>(); _voiceBtn.targetGraphic = vImg;
        var vT = MakeTMP(vGO.transform,"MIC",SU(13),Hex("9F7AEA"),true);
        StretchFill(vT.GetComponent<RectTransform>()); vT.alignment = TextAlignmentOptions.Center;
        _voiceBtn.onClick.AddListener(ToggleVoice);

        var sGO  = MakeGO("SendBtn",bar.transform);
        var sImg = sGO.AddComponent<Image>(); sImg.color = Hex("7C3AED");
        ApplyRounded(sImg);
        var sRT  = sGO.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(1,0.5f); sRT.anchorMax = new Vector2(1,0.5f);
        sRT.pivot = new Vector2(1,0.5f);
        sRT.anchoredPosition = new Vector2(-SU(12),0); sRT.sizeDelta = new Vector2(SU(62),SU(62));
        _sendBtn = sGO.AddComponent<Button>(); _sendBtn.targetGraphic = sImg;
        var sT = MakeTMP(sGO.transform,"Send",SU(14),Color.white,true);
        StretchFill(sT.GetComponent<RectTransform>()); sT.alignment = TextAlignmentOptions.Center;
        _sendBtn.onClick.AddListener(OnSendClicked);
    }

    // ═══════════════════════════════════════════════════════════
    //  BUBBLE FACTORY
    //  ★ Each bubble row gets an EXPLICIT sizeDelta.y measured from
    //    the actual text — no ContentSizeFitter on the row itself
    //    (conflicts with VLG when childControlHeight=false).
    // ═══════════════════════════════════════════════════════════
    float MeasureTextHeight(string text, float fontSize, float width)
    {
        var go  = new GameObject("_tmpmeasure");
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode     = TextOverflowModes.Overflow;
        tmp.ForceMeshUpdate();
        float h = tmp.GetPreferredValues(text, width, 99999f).y;
        Destroy(go);
        return Mathf.Max(h, fontSize * 1.5f);
    }

    GameObject CreateUserBubbleRow(Transform parent, string text)
    {
        float maxW  = SU(360);
        float textH = MeasureTextHeight(text, SU(19), maxW);
        float padV  = SU(28);
        float rowH  = textH + padV + SU(16);

        var root   = MakeGO("UserBubble", parent);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0, 1);
        rootRT.anchorMax = new Vector2(1, 1);
        rootRT.pivot     = new Vector2(0.5f, 1f);
        rootRT.sizeDelta = new Vector2(0, rowH);
        var rootLE = root.AddComponent<LayoutElement>();
        rootLE.preferredHeight = rowH;
        rootLE.minHeight       = rowH;
        rootLE.flexibleWidth   = 1f;
        rootLE.flexibleHeight  = 0f;

        var hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment         = TextAnchor.UpperRight;
        hlg.padding                = new RectOffset(SI(70),SI(14),SI(6),SI(6));
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        var bub    = MakeGO("Bubble", root.transform);
        var bubImg = bub.AddComponent<Image>(); bubImg.color = Hex("6D28D9");
        ApplyRounded(bubImg);
        bub.GetComponent<RectTransform>().sizeDelta = new Vector2(maxW + SU(36), textH + padV);

        var msg  = MakeGO("MessageText", bub.transform);
        var mt   = msg.AddComponent<TextMeshProUGUI>();
        mt.text             = text;
        mt.fontSize         = SU(19);
        mt.color            = Color.white;
        mt.textWrappingMode = TextWrappingModes.Normal;
        mt.overflowMode     = TextOverflowModes.Overflow;
        var msgRT = msg.GetComponent<RectTransform>();
        msgRT.anchorMin = Vector2.zero; msgRT.anchorMax = Vector2.one;
        msgRT.offsetMin = new Vector2(SU(18),SU(14));
        msgRT.offsetMax = new Vector2(-SU(18),-SU(14));

        return root;
    }

    GameObject CreateBotBubbleRow(Transform parent, string text)
    {
        float maxW  = SU(360);
        float textH = MeasureTextHeight(text, SU(19), maxW);
        float padV  = SU(28);
        float rowH  = textH + padV + SU(16);

        var root   = MakeGO("BotBubble", parent);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0, 1);
        rootRT.anchorMax = new Vector2(1, 1);
        rootRT.pivot     = new Vector2(0.5f, 1f);
        rootRT.sizeDelta = new Vector2(0, rowH);
        var rootLE = root.AddComponent<LayoutElement>();
        rootLE.preferredHeight = rowH;
        rootLE.minHeight       = rowH;
        rootLE.flexibleWidth   = 1f;
        rootLE.flexibleHeight  = 0f;

        var hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment         = TextAnchor.UpperLeft;
        hlg.padding                = new RectOffset(SI(14),SI(70),SI(6),SI(6));
        hlg.spacing                = SU(10);
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        var av    = MakeGO("Avatar", root.transform);
        av.AddComponent<Image>().color = Hex("7C3AED");
        av.GetComponent<RectTransform>().sizeDelta = new Vector2(SU(34),SU(34));
        var avT = MakeTMP(av.transform,"AI",SU(11),Color.white,true);
        StretchFill(avT.GetComponent<RectTransform>());
        avT.alignment = TextAlignmentOptions.Center;

        // ★ Bubble colour: medium purple — clearly different from near-black background
        var bub    = MakeGO("Bubble", root.transform);
        var bubImg = bub.AddComponent<Image>(); bubImg.color = Hex("3B1F7A");
        ApplyRounded(bubImg);
        bub.GetComponent<RectTransform>().sizeDelta = new Vector2(maxW + SU(36), textH + padV);

        // ★ Text colour: pure white for maximum readability
        var msg  = MakeGO("MessageText", bub.transform);
        var mt   = msg.AddComponent<TextMeshProUGUI>();
        mt.text             = text;
        mt.fontSize         = SU(19);
        mt.color            = Color.white;
        mt.textWrappingMode = TextWrappingModes.Normal;
        mt.overflowMode     = TextOverflowModes.Overflow;
        var msgRT = msg.GetComponent<RectTransform>();
        msgRT.anchorMin = Vector2.zero; msgRT.anchorMax = Vector2.one;
        msgRT.offsetMin = new Vector2(SU(18),SU(14));
        msgRT.offsetMax = new Vector2(-SU(18),-SU(14));

        return root;
    }

    // ═══════════════════════════════════════════════════════════
    //  OPEN / CLOSE
    // ═══════════════════════════════════════════════════════════
    public void OpenChatbot()
    {
        OpenChatbotInternal(false,
            "Hi! I am your AI Teacher for Machine Learning.\n\n" +
            "Ask me anything about ML concepts — logistic regression, " +
            "neural networks, gradient descent, decision trees, and more!\n\n" +
            "Type your question or press MIC to speak.");
    }

    public void CloseChatbot() => CloseChatbotInternal(true, true);

    void OpenChatbotInternal(bool openedByAttention, string greetingText)
    {
        if (tutorialScreen != null) tutorialScreen.SetActive(false);
        _chatScreen.SetActive(true);
        if (teachingManager != null) teachingManager.PauseLesson();
        _openedByAttention = openedByAttention;
        ClearChat();
        SetSuggestionsInteractable(true);
        StartCoroutine(SpawnGreeting(greetingText));
    }

    // ★ Wait 2 frames after SetActive so the Canvas layout is ready
    IEnumerator SpawnGreeting(string greetingText)
    {
        yield return null;
        yield return null;
        SpawnMetaLine("TODAY : SESSION 1");
        yield return new WaitForSecondsRealtime(0.08f);
        SpawnBotBubbleImmediate(greetingText);
        yield return new WaitForSecondsRealtime(0.3f);
        SpawnSuggestionChipImmediate("What is the sigmoid function and why do we use it?");
        yield return new WaitForSecondsRealtime(0.15f);
        SpawnSuggestionChipImmediate("How does the decision boundary work?");
        yield return new WaitForSecondsRealtime(0.15f);
        SpawnSuggestionChipImmediate("Explain gradient descent in simple words.");
        RebuildLayout();
        StartCoroutine(ScrollBottom());
    }

    void CloseChatbotInternal(bool resumeLesson, bool showTutorial)
    {
        if (_isRecording) { Microphone.End(null); _isRecording = false; }
        if (_audioSource != null && _audioSource.isPlaying) _audioSource.Stop();
        if (_countdownCo != null) { StopCoroutine(_countdownCo); _countdownCo = null; }
        if (_typingIndicator != null) _typingIndicator.SetActive(false);
        _chatScreen.SetActive(false);
        _openedByAttention = false;
        if (showTutorial  && tutorialScreen  != null) tutorialScreen.SetActive(true);
        if (resumeLesson  && teachingManager != null) teachingManager.ResumeLesson();
    }

    void ShowTutorial()
    {
        if (tutorialScreen != null) tutorialScreen.SetActive(true);
        if (_chatScreen    != null) _chatScreen.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════
    //  ATTENTION STATE
    // ═══════════════════════════════════════════════════════════
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
                _badgeText.text  = "● FOCUSED";   _badgeText.color = Hex("4ADE80");
                _badgeBg.color   = new Color(0.08f,0.35f,0.12f,0.85f); break;
            case "UNFOCUSED":
                _badgeText.text  = "● UNFOCUSED"; _badgeText.color = Hex("FACC15");
                _badgeBg.color   = new Color(0.35f,0.28f,0.04f,0.85f); break;
            case "DROWSY":
                _badgeText.text  = "● DROWSY";    _badgeText.color = Hex("F87171");
                _badgeBg.color   = new Color(0.35f,0.06f,0.06f,0.85f); break;
        }

        if (norm == _lastAttentionState) return;
        float now = Time.time;
        bool inDebounce = _lastAttentionChangeTime > -100f &&
                          (now - _lastAttentionChangeTime) < attentionDebounceSeconds;
        if (norm != "DROWSY" && inDebounce) return;
        _lastAttentionState = norm; _lastAttentionChangeTime = now;

        bool chatOpen = _chatScreen != null && _chatScreen.activeSelf;
        switch (norm)
        {
            case "FOCUSED":
                if (teachingManager != null && teachingManager.IsInBreak && !chatOpen)
                    teachingManager.ResumeLesson();
                if (_openedByAttention && chatOpen) CloseChatbotInternal(true,true);
                else if (!chatOpen && tutorialScreen != null) tutorialScreen.SetActive(true);
                break;
            case "UNFOCUSED":
                if (!chatOpen)
                    OpenChatbotInternal(true,
                        "I noticed you might be a little unfocused. " +
                        "What part of the lesson is confusing? Ask me and I'll help.");
                break;
            case "DROWSY":
                if (chatOpen) CloseChatbotInternal(false,false);
                _openedByAttention = false;
                if (tutorialScreen  != null) tutorialScreen.SetActive(false);
                if (teachingManager != null) teachingManager.BreakLesson();
                SetSuggestionsInteractable(false);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  SEND TEXT
    // ═══════════════════════════════════════════════════════════
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

        SpawnUserBubbleImmediate(q);
        _inputField.text      = "";
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

    // ═══════════════════════════════════════════════════════════
    //  VOICE
    // ═══════════════════════════════════════════════════════════
    void ToggleVoice() { if (!_isRecording) StartVoice(); else StopVoice(); }

    void StartVoice()
    {
        if (Microphone.devices.Length == 0)
        { SpawnBotBubbleImmediate("No microphone found on this device."); return; }
        _isRecording  = true;
        _recordedClip = Microphone.Start(null, false, MAX_RECORD_SECS, SAMPLE_RATE);
        SetVoiceColor(true);
        SpawnUserBubbleImmediate("🎤 Recording... tap MIC again to stop.");
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
        Microphone.End(null); SetVoiceColor(false);
        if (_recordedClip == null || last <= 0)
        { SpawnBotBubbleImmediate("I couldn't hear anything. Please try again."); return; }
        float[] samples = new float[last * _recordedClip.channels];
        _recordedClip.GetData(samples, 0);
        byte[] wav = ConvertToWav(samples, _recordedClip.channels, SAMPLE_RATE);
        ShowTyping(true);
        StartCoroutine(AskGeminiAudio(wav));
    }

    void SetVoiceColor(bool rec)
    {
        var img = _voiceBtn?.GetComponent<Image>();
        if (img) img.color = rec ? new Color(0.85f,0.15f,0.15f,1f) : Hex("1A1535");
    }

    // ═══════════════════════════════════════════════════════════
    //  GEMINI
    // ═══════════════════════════════════════════════════════════
    IEnumerator AskGemini(string question)
    {
        string body =
            "{\"system_instruction\":{\"parts\":[{\"text\":\"" + EscapeJson(SYSTEM_PROMPT) + "\"}]}," +
            "\"contents\":[{\"role\":\"user\",\"parts\":[{\"text\":\"" + EscapeJson(question) + "\"}]}]," +
            "\"generationConfig\":{\"maxOutputTokens\":300,\"temperature\":0.7}}";
        yield return StartCoroutine(SendToGemini(Encoding.UTF8.GetBytes(body)));
    }

    IEnumerator AskGeminiAudio(byte[] wav)
    {
        string b64 = Convert.ToBase64String(wav);
        string ins = "The student asked a question via voice recording. Transcribe what they said and answer accordingly.";
        string body =
            "{\"system_instruction\":{\"parts\":[{\"text\":\"" + EscapeJson(SYSTEM_PROMPT) + "\"}]}," +
            "\"contents\":[{\"role\":\"user\",\"parts\":[" +
            "{\"inline_data\":{\"mime_type\":\"audio/wav\",\"data\":\"" + b64 + "\"}}," +
            "{\"text\":\"" + EscapeJson(ins) + "\"}]}]," +
            "\"generationConfig\":{\"maxOutputTokens\":300,\"temperature\":0.7}}";
        yield return StartCoroutine(SendToGemini(Encoding.UTF8.GetBytes(body)));
    }

    IEnumerator SendToGemini(byte[] body)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey == "PASTE_YOUR_GEMINI_API_KEY_HERE")
        {
            ShowTyping(false); _isRequestRunning = false; _sendBtn.interactable = true;
            SetSuggestionsInteractable(true);
            SpawnBotBubbleImmediate("No API key set. Paste your Gemini API key in the Inspector.");
            yield break;
        }

        using var req = new UnityWebRequest(_apiUrl + "?key=" + apiKey, "POST");
        req.uploadHandler   = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type","application/json");
        yield return req.SendWebRequest();

        Debug.Log($"[Gemini] HTTP {req.responseCode} | {req.result}");
        ShowTyping(false);

        if (req.responseCode == 403)
        {
            _isRequestRunning = false; _sendBtn.interactable = true; SetSuggestionsInteractable(true);
            SpawnBotBubbleImmediate("API key error (403). Please check your Gemini API key in Inspector.");
        }
        else if (req.responseCode == 429)
        {
            int rs = GetRetryAfterSeconds(req, 30);
            _requestCooldown  = Mathf.Max(8f, rs); _lastRequestTime = Time.time;
            _isRequestRunning = false; _sendBtn.interactable = false; SetSuggestionsInteractable(false);
            if (_countdownCo != null) StopCoroutine(_countdownCo);
            _countdownCo = StartCoroutine(Countdown(_requestCooldown));
            SpawnBotBubbleImmediate("Rate-limited (429). Please wait ~" + Mathf.CeilToInt(_requestCooldown) + "s.");
        }
        else if (req.result == UnityWebRequest.Result.Success)
        {
            string answer = ParseResponse(req.downloadHandler.text);
            Debug.Log("[Gemini] answer len=" + answer.Length + " : " + answer.Substring(0,Mathf.Min(80,answer.Length)));
            _lastAnswer = answer; _isRequestRunning = false; _requestCooldown = 5f;
            _sendBtn.interactable = true; SetSuggestionsInteractable(true);
            if (_chatScreen != null && !_chatScreen.activeSelf) _chatScreen.SetActive(true);
            SpawnBotBubbleImmediate(answer);
            StartCoroutine(Speak(answer));
        }
        else
        {
            Debug.LogError("[Gemini] Error: " + req.error);
            _isRequestRunning = false; _sendBtn.interactable = true; SetSuggestionsInteractable(true);
            SpawnBotBubbleImmediate("Connection failed. Check your internet and try again.");
        }
    }

    int GetRetryAfterSeconds(UnityWebRequest req, int fallback)
    {
        var h = req?.GetResponseHeaders();
        if (h == null) return fallback;
        foreach (var kv in h)
            if (string.Equals(kv.Key,"Retry-After",StringComparison.OrdinalIgnoreCase))
                if (int.TryParse(kv.Value, out int s) && s > 0) return s;
        return fallback;
    }

    [Serializable] class GeminiResponse  { public GeminiCandidate[] candidates; }
    [Serializable] class GeminiCandidate { public GeminiContent content; }
    [Serializable] class GeminiContent   { public GeminiPart[] parts; }
    [Serializable] class GeminiPart      { public string text; }

    string ParseResponse(string json)
    {
        Debug.Log("[Gemini RAW] " + json);
        try
        {
            var root = JsonUtility.FromJson<GeminiResponse>(json);
            if (root?.candidates == null || root.candidates.Length == 0) return FallbackParse(json);
            var parts = root.candidates[0]?.content?.parts;
            if (parts == null || parts.Length == 0) return "No response. Please try again.";
            string t = parts[0].text;
            return string.IsNullOrEmpty(t) ? "Empty response. Please try again." : t.Trim();
        }
        catch { return FallbackParse(json); }
    }

    string FallbackParse(string json)
    {
        try
        {
            int i = json.LastIndexOf("\"text\":");
            if (i < 0) return "No response received.";
            int s = json.IndexOf('"', i + 7); if (s < 0) return "No response received."; s++;
            var sb = new StringBuilder(); int pos = s;
            while (pos < json.Length)
            {
                char c = json[pos];
                if (c == '\\' && pos+1 < json.Length) {
                    char n = json[pos+1];
                    switch(n){case 'n':sb.Append('\n');pos+=2;continue;case 'r':sb.Append('\r');pos+=2;continue;
                              case 't':sb.Append('\t');pos+=2;continue;case '"':sb.Append('"');pos+=2;continue;
                              case '\\':sb.Append('\\');pos+=2;continue;default:sb.Append(n);pos+=2;continue;}
                }
                if (c == '"') break; sb.Append(c); pos++;
            }
            string r = sb.ToString().Trim();
            return string.IsNullOrEmpty(r) ? "Empty response from Gemini." : r;
        }
        catch { return "Could not read response."; }
    }

    // ═══════════════════════════════════════════════════════════
    //  IMMEDIATE SPAWN METHODS
    //  ★ No FadeIn, no coroutine delay — bubbles appear instantly
    //    with correct measured height so the VLG stacks them visibly.
    // ═══════════════════════════════════════════════════════════
    void SpawnUserBubbleImmediate(string text)
    {
        if (_contentArea == null) { Debug.LogError("[Bubble] _contentArea null"); return; }
        CreateUserBubbleRow(_contentArea, text);
        RebuildLayout();
        StartCoroutine(ScrollBottom());
    }

    void SpawnBotBubbleImmediate(string text)
    {
        if (_contentArea == null) { Debug.LogError("[Bubble] _contentArea null"); return; }
        if (_chatScreen != null && !_chatScreen.activeSelf) _chatScreen.SetActive(true);
        CreateBotBubbleRow(_contentArea, text);
        Debug.Log("[Bubble] bot bubble spawned, text len=" + text.Length);
        RebuildLayout();
        StartCoroutine(ScrollBottom());
    }

    void SpawnMetaLine(string text)
    {
        if (_contentArea == null) return;
        var go  = MakeGO("MetaLine", _contentArea);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, SU(28));
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = SU(28);
        le.minHeight = SU(28);
        le.flexibleHeight = 0f;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = SU(14); tmp.color = Hex("9F7AEA");
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
    }

    void SpawnSuggestionChipImmediate(string text)
    {
        if (_contentArea == null) return;

        var container = MakeGO("SuggestionContainer", _contentArea);
        container.GetComponent<RectTransform>().sizeDelta = new Vector2(0, SU(80));
        var le = container.AddComponent<LayoutElement>();
        le.preferredHeight = SU(80);
        le.minHeight = SU(80);
        le.flexibleHeight = 0f;

        var hlg = container.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        var chipGO = new GameObject("SuggestionChip");
        chipGO.transform.SetParent(container.transform, false);
        var chipRT = chipGO.AddComponent<RectTransform>();
        chipRT.sizeDelta = new Vector2(SU(640), SU(76));

        var img = chipGO.AddComponent<Image>(); img.color = Hex("7C3AED"); ApplyRounded(img);
        var btn = chipGO.AddComponent<Button>(); btn.targetGraphic = img;
        string cap = text;
        btn.onClick.AddListener(() => OnSuggestionClicked(cap));
        _suggestionButtons.Add(btn);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(chipGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero; labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(SU(14),SU(8)); labelRT.offsetMax = new Vector2(-SU(14),-SU(8));

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = SU(18); tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.overflowMode = TextOverflowModes.Ellipsis; tmp.raycastTarget = false;
    }

    void OnSuggestionClicked(string text)
    {
        if (_isRequestRunning || _inputField == null || string.IsNullOrWhiteSpace(text)) return;
        if (_isRecording) { Microphone.End(null); _isRecording = false; SetVoiceColor(false); }
        _inputField.text = text;
        OnSendClicked();
    }

    void SetSuggestionsInteractable(bool e)
    {
        foreach (var b in _suggestionButtons) if (b != null) b.interactable = e;
    }

    void ClearChat()
    {
        _suggestionButtons.Clear();
        if (_contentArea == null) return;
        foreach (Transform child in _contentArea) Destroy(child.gameObject);
    }

    // ═══════════════════════════════════════════════════════════
    //  LAYOUT + SCROLL HELPERS
    // ═══════════════════════════════════════════════════════════
    void RebuildLayout()
    {
        if (_contentArea == null) return;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentArea);
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
            t += Time.unscaledDeltaTime;
            _scrollRect.verticalNormalizedPosition =
                Mathf.Lerp(from, 0f, Mathf.SmoothStep(0,1,t/dur));
            yield return null;
        }
        _scrollRect.verticalNormalizedPosition = 0f;
    }

    void ShowTyping(bool show)
    {
        if (_typingIndicator != null) _typingIndicator.SetActive(show);
        if (show) StartCoroutine(ScrollBottom());
    }

    // ═══════════════════════════════════════════════════════════
    //  TTS
    // ═══════════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════
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
        tmp.text = text; tmp.fontSize = size; tmp.color = color;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        return tmp;
    }

    GameObject CreatePanel(Transform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.color = color; ApplyRounded(img);
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
            Convert.ToInt32(hex.Substring(0,2),16)/255f,
            Convert.ToInt32(hex.Substring(2,2),16)/255f,
            Convert.ToInt32(hex.Substring(4,2),16)/255f,
            Convert.ToInt32(hex.Substring(6,2),16)/255f);
    }

    static string EscapeJson(string s) =>
        s.Replace("\\","\\\\").Replace("\"","\\\"")
         .Replace("\n","\\n").Replace("\r","\\r").Replace("\t","\\t");

    byte[] ConvertToWav(float[] samples, int channels, int sampleRate)
    {
        int bc = samples.Length * 2, total = 44 + bc;
        byte[] wav = new byte[total];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(wav,0); BitConverter.GetBytes(total-8).CopyTo(wav,4);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(wav,8); Encoding.ASCII.GetBytes("fmt ").CopyTo(wav,12);
        BitConverter.GetBytes(16).CopyTo(wav,16);      BitConverter.GetBytes((short)1).CopyTo(wav,20);
        BitConverter.GetBytes((short)channels).CopyTo(wav,22);
        BitConverter.GetBytes(sampleRate).CopyTo(wav,24);
        BitConverter.GetBytes(sampleRate*channels*2).CopyTo(wav,28);
        BitConverter.GetBytes((short)(channels*2)).CopyTo(wav,32);
        BitConverter.GetBytes((short)16).CopyTo(wav,34);
        Encoding.ASCII.GetBytes("data").CopyTo(wav,36); BitConverter.GetBytes(bc).CopyTo(wav,40);
        int off = 44;
        foreach (float s in samples)
        { short v=(short)(Mathf.Clamp(s,-1f,1f)*short.MaxValue); BitConverter.GetBytes(v).CopyTo(wav,off); off+=2; }
        return wav;
    }
}

// ═══════════════════════════════════════════════════════════════
//  TYPING DOTS ANIMATION
// ═══════════════════════════════════════════════════════════════
public class TypingDotsAnim : MonoBehaviour
{
    private Image _d0, _d1, _d2;
    private float _t;
    public void Init(Image d0, Image d1, Image d2) { _d0=d0; _d1=d1; _d2=d2; }
    void Update()
    {
        if (_d0 == null) return;
        _t += Time.deltaTime * 3f;
        Color c = new Color(0.49f,0.23f,0.93f,1f);
        _d0.color = new Color(c.r,c.g,c.b,Mathf.Abs(Mathf.Sin(_t)));
        _d1.color = new Color(c.r,c.g,c.b,Mathf.Abs(Mathf.Sin(_t+0.6f)));
        _d2.color = new Color(c.r,c.g,c.b,Mathf.Abs(Mathf.Sin(_t+1.2f)));
    }
}

// Compatibility shim
public class AIDoubtSolver : ChatbotUIBuilder { }