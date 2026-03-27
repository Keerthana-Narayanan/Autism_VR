using System;
using UnityEngine;
using Unity.InferenceEngine;



/// <summary>
/// Runs the exported BiLSTM ONNX model in Unity using Sentis (com.unity.ai.inference).
///
/// Model expectation (from your notebook):
/// - Input:  (batch=1, timesteps=512, channels=4) float32
/// - Output: logits (batch=1, classes=3) float32
/// - Class mapping: 0 Focused, 1 Unfocused, 2 Drowsy
///
/// Hook this to your Muse2 stream:
/// - Feed a rolling window of the last 512 samples for TP9/AF7/AF8/TP10 (normalized).
/// - Call Predict(window) whenever you want a new label (e.g., every 1s for 50% overlap).
/// </summary>
public class AttentionSentisRunner : MonoBehaviour
{
    [Header("Model")]
    public ModelAsset onnxModel;

    [Header("Output")]
    public AttentionStateBridge bridge;
    public bool autoFindBridge = true;

    [Header("Inference timing (optional)")]
    public bool autoPredict = false;
    public float predictEverySeconds = 1.0f;

    [Header("Input normalization (z-score)")]
    [Tooltip("Per-channel mean used during training. Order: TP9, AF7, AF8, TP10")]
    public float[] mean = new float[4] { 0, 0, 0, 0 };
    [Tooltip("Per-channel std used during training. Order: TP9, AF7, AF8, TP10")]
    public float[] std = new float[4] { 1, 1, 1, 1 };

    [Header("Debug / Testing")]
    public bool enableKeyboardTesting = false;

    // Rolling buffer: 512 rows, 4 cols.
    // For manual/debug use or external filling.
    public float[] debugWindow512x4 = new float[512 * 4];

    Worker _worker;
    Model _runtimeModel;
    float _nextPredictTime;
    readonly float[] _rollingWindow = new float[512 * 4];
    int _samplesInWindow = 0;

    void Start()
    {
        if (bridge == null && autoFindBridge)
            bridge = FindFirstObjectByType<AttentionStateBridge>();

        if (onnxModel == null)
        {
            Debug.LogError("[AttentionSentisRunner] Missing onnxModel. Import ONNX as a ModelAsset and assign it.");
            enabled = false;
            return;
        }

        _runtimeModel = ModelLoader.Load(onnxModel);
        _worker = new Worker(_runtimeModel, BackendType.GPUCompute);
        _nextPredictTime = Time.time + predictEverySeconds;
    }

    void OnDestroy()
    {
        _worker?.Dispose();
        _worker = null;
    }

    void Update()
    {
        if (enableKeyboardTesting && Input.GetKeyDown(KeyCode.P))
        {
            // One-shot test using the current debugWindow.
            Predict(debugWindow512x4);
        }

        if (!autoPredict) return;
        if (Time.time < _nextPredictTime) return;
        _nextPredictTime = Time.time + Mathf.Max(0.05f, predictEverySeconds);

        // Predict only after we have at least 512 samples in the rolling window.
        if (_samplesInWindow >= 512)
            Predict(_rollingWindow);
    }

    /// <summary>
    /// Feed one new Muse2 sample in channel order: TP9, AF7, AF8, TP10.
    /// Keep calling this at 256 Hz from your device stream.
    /// </summary>
    public void PushSample(float tp9, float af7, float af8, float tp10)
    {
        // Shift left by one timestep (4 floats), append latest timestep at the end.
        Array.Copy(_rollingWindow, 4, _rollingWindow, 0, _rollingWindow.Length - 4);
        int tail = _rollingWindow.Length - 4;
        _rollingWindow[tail + 0] = tp9;
        _rollingWindow[tail + 1] = af7;
        _rollingWindow[tail + 2] = af8;
        _rollingWindow[tail + 3] = tp10;

        if (_samplesInWindow < 512) _samplesInWindow++;
    }

    /// <summary>
    /// Predicts attention class from a 512x4 window flattened row-major: [t0c0,t0c1,t0c2,t0c3,t1c0,...]
    /// </summary>
    public void Predict(float[] window512x4)
    {
        if (_worker == null) return;
        if (window512x4 == null || window512x4.Length != 512 * 4)
        {
            Debug.LogError("[AttentionSentisRunner] window must be length 2048 (512*4).");
            return;
        }

        // Create input tensor with shape (1,512,4)
        using var input = new Tensor<float>(new TensorShape(1, 512, 4));
        int i = 0;
        for (int t = 0; t < 512; t++)
        {
            for (int c = 0; c < 4; c++)
            {
                float v = window512x4[i++];
                float m = (mean != null && mean.Length == 4) ? mean[c] : 0f;
                float s = (std  != null && std.Length == 4) ? std[c]  : 1f;
                if (Mathf.Abs(s) < 1e-6f) s = 1f;
                input[0, t, c] = (v - m) / s;
            }
        }

        _worker.Schedule(input);

        // Assume single output. Read logits (1,3).
        var output = _worker.PeekOutput() as Tensor<float>;
        if (output == null)
        {
            Debug.LogError("[AttentionSentisRunner] Model output is not Tensor<float>.");
            return;
        }

        using var cpu = output.ReadbackAndClone() as Tensor<float>;
        if (cpu == null)
        {
            Debug.LogError("[AttentionSentisRunner] Output readback failed.");
            return;
        }

        float a0 = cpu[0, 0];
        float a1 = cpu[0, 1];
        float a2 = cpu[0, 2];

        int cls = ArgMax3(a0, a1, a2);

        if (bridge != null)
            bridge.OnAttentionClass(cls);
    }

    static int ArgMax3(float a0, float a1, float a2)
    {
        if (a1 > a0 && a1 >= a2) return 1;
        if (a2 > a0 && a2 > a1)  return 2;
        return 0;
    }
}

