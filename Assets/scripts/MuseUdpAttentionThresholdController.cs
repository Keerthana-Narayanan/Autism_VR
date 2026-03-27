using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// Receives UDP attention labels from your Python streamer:
///   "Focused" | "Unfocused"
///
/// Triggers:
///  - UNFOCUSED streak >= N: open AI chat (+ optional pause lesson)
///
/// This script purposely does NOT use the prefab/UI chat rendering — it only drives behaviors.
/// </summary>
public class MuseUdpAttentionThresholdController : MonoBehaviour
{
    [Header("UDP")]
    public string listenHost = "0.0.0.0";
    public int listenPort = 12345;
    public bool autoFind = true;

    [Header("Thresholds (consecutive messages)")]
    public int unfocusedStreakThreshold = 20;

    [Header("Timeout")]
    [Tooltip("If no UDP messages arrive within this time, reset streaks.")]
    public float staleResetSeconds = 60f;

    [Header("Debug")]
    public bool logStreakProgress = false;

    [Header("Actions")]
    public bool pauseLessonOnUnfocused = true;
    public bool resumeLessonOnFocused = true;

    [Header("Chat popup rate limit")]
    [Tooltip("Minimum seconds between auto-opening the chat due to UNFOCUSED streak.")]
    public float chatPopupCooldownSeconds = 20f;

    [Header("References")]
    public ChatbotUIBuilder chatbotUI;
    public TeachingAudioManager teachingManager;

    [Header("Optional: disable other attention approach")]
    [Tooltip("Disables AttentionSentisRunner and AttentionStateBridge components to prevent double-driving.")]
    public bool disableSentisComponents = true;

    UdpClient _udp;
    readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
    IPEndPoint _remoteAny;

    int _unfocusedStreak;
    float _lastMessageTime = -999f;

    bool _chatOpenedForUnfocused;
    float _lastChatPopupTime = -999f;

    void Start()
    {
        if (autoFind)
        {
            if (chatbotUI == null) chatbotUI = FindFirstObjectByType<ChatbotUIBuilder>();
            if (teachingManager == null) teachingManager = FindFirstObjectByType<TeachingAudioManager>();
        }

        // IMPORTANT: Don't create the rest dialog at startup.
        // Creating a full-screen overlay Canvas can accidentally block tutorial UI in some scenes.
        // We'll lazy-create it only when drowsy threshold is reached.

        if (disableSentisComponents)
        {
            var sentis = FindObjectsOfType<AttentionSentisRunner>();
            foreach (var s in sentis) s.enabled = false;
            var bridge = FindObjectsOfType<AttentionStateBridge>();
            foreach (var b in bridge) b.enabled = false;
        }

        StartUdp();
    }

    void StartUdp()
    {
        _udp = new UdpClient(listenPort);
        _remoteAny = new IPEndPoint(IPAddress.Any, listenPort);

        _udp.BeginReceive(OnUdpReceived, null);
        Debug.Log($"[MuseUdp] Listening on UDP {listenHost}:{listenPort}");
    }

    void OnUdpReceived(IAsyncResult ar)
    {
        try
        {
            if (_udp == null) return;

            byte[] data = _udp.EndReceive(ar, ref _remoteAny);
            string msg = Encoding.UTF8.GetString(data).Trim();
            if (!string.IsNullOrEmpty(msg))
                _queue.Enqueue(msg);

            _udp.BeginReceive(OnUdpReceived, null);
        }
        catch (ObjectDisposedException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[MuseUdp] Receive error: " + ex.Message);
            try { _udp.BeginReceive(OnUdpReceived, null); } catch { }
        }
    }

    void Update()
    {
        // If chat was opened due to unfocused and user closes it, allow re-open on next streak.
        if (_chatOpenedForUnfocused)
        {
            if (chatbotUI == null || !chatbotUI.IsChatOpen())
            {
                _chatOpenedForUnfocused = false;
                _unfocusedStreak = 0;
            }
        }

        // Reset streaks if messages stop coming.
        if (Time.time - _lastMessageTime > staleResetSeconds)
        {
            _unfocusedStreak = 0;
            _chatOpenedForUnfocused = false;
        }

        while (_queue.TryDequeue(out var msg))
        {
            _lastMessageTime = Time.time;
            ProcessState(msg);
        }
    }

    void ProcessState(string stateRaw)
    {
        string s = stateRaw.Trim();
        // Normalize common variants
        if (s.Equals("Focused", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("FOCUSED", StringComparison.OrdinalIgnoreCase))
            HandleFocused();
        else if (s.Equals("Unfocused", StringComparison.OrdinalIgnoreCase) ||
                 s.Equals("UNFOCUSED", StringComparison.OrdinalIgnoreCase))
            HandleUnfocused();
        // Drowsy is mapped to Unfocused in the Python streamer; keep this fallback just in case.
        else if (s.Equals("Drowsy", StringComparison.OrdinalIgnoreCase) ||
                 s.Equals("DROWSY", StringComparison.OrdinalIgnoreCase))
            HandleUnfocused();
        else
            Debug.LogWarning("[MuseUdp] Unknown state message: " + stateRaw);
    }

    void HandleFocused()
    {
        _unfocusedStreak = 0;
        _chatOpenedForUnfocused = false;

        if (resumeLessonOnFocused && teachingManager != null && !teachingManager.IsInBreak)
            teachingManager.ResumeLesson();
    }

    void HandleUnfocused()
    {
        _unfocusedStreak++;

        if (logStreakProgress)
            Debug.Log($"[MuseUdp] UNFOCUSED streak={_unfocusedStreak}/{unfocusedStreakThreshold}");

        if (_chatOpenedForUnfocused) return;
        if (_unfocusedStreak < unfocusedStreakThreshold) return;

        // Rate-limit how often we auto-open chat, even if the user keeps going unfocused.
        if (Time.time - _lastChatPopupTime < Mathf.Max(0f, chatPopupCooldownSeconds))
        {
            if (logStreakProgress)
                Debug.Log($"[MuseUdp] UNFOCUSED threshold met but in cooldown ({Time.time - _lastChatPopupTime:0.0}s/{chatPopupCooldownSeconds:0.0}s).");
            return;
        }

        _chatOpenedForUnfocused = true;
        _lastChatPopupTime = Time.time;
        Debug.Log($"[MuseUdp] UNFOCUSED streak reached ({_unfocusedStreak}). Opening chat...");

        if (pauseLessonOnUnfocused && teachingManager != null)
            teachingManager.PauseLesson();

        if (chatbotUI != null)
            chatbotUI.OpenChatbot();
        else
            Debug.LogWarning("[MuseUdp] chatbotUI reference is missing; cannot open chat.");

        // Reset streak after acting so we require another full streak before reopening (after cooldown).
        _unfocusedStreak = 0;
    }

    void OnDestroy()
    {
        try { _udp?.Close(); } catch { }
        _udp = null;
    }
}

