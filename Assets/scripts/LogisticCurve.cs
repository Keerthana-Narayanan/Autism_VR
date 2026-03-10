using UnityEngine;
using TMPro;

// ============================================================
// LogisticCurve.cs
// Attach this to a GameObject that has a LineRenderer component.
// This draws the sigmoid curve in real-time.
// ============================================================

public class LogisticCurve : MonoBehaviour
{
    [Header("Line Renderers")]
    public LineRenderer curveRenderer;        // The sigmoid curve line
    public LineRenderer decisionLineRenderer; // Vertical dashed boundary line
    public LineRenderer xAxisRenderer;        // X axis
    public LineRenderer yAxisRenderer;        // Y axis

    [Header("Graph Settings")]
    public int resolution = 300;   // More = smoother curve
    public float xMin = -6f;
    public float xMax = 6f;
    public float graphScaleX = 1.0f; // World units per graph unit
    public float graphScaleY = 2.0f; // World units per 0-1 range

    [Header("Logistic Regression Parameters")]
    [Range(-4f, 4f)] public float weight = 1f;
    [Range(-4f, 4f)] public float bias = 0f;

    [Header("UI Text (TextMeshPro)")]
    public TextMeshProUGUI formulaText;
    public TextMeshProUGUI weightValueText;
    public TextMeshProUGUI biasValueText;
    public TextMeshProUGUI boundaryValueText;
    public TextMeshProUGUI annotationText;
    public TextMeshProUGUI probabilityText;

    [Header("Colors")]
    public Color curveColor = new Color(0f, 0.9f, 1f);       // Cyan
    public Color boundaryColor = new Color(1f, 0.4f, 0.4f);  // Red dashed
    public Color axisColor = new Color(0.8f, 0.8f, 0.8f);    // Light gray

    void Start()
    {
        SetupLineRenderers();
        DrawAxes();
    }

    void Update()
    {
        DrawSigmoidCurve();
        DrawDecisionBoundary();
        UpdateAllUIText();
    }

    // ── Setup ─────────────────────────────────────────────────
    void SetupLineRenderers()
    {
        // Curve line
        curveRenderer.startColor = curveColor;
        curveRenderer.endColor = curveColor;
        curveRenderer.startWidth = 0.06f;
        curveRenderer.endWidth = 0.06f;
        curveRenderer.useWorldSpace = true;

        // Decision boundary line
        if (decisionLineRenderer != null)
        {
            decisionLineRenderer.startColor = boundaryColor;
            decisionLineRenderer.endColor = boundaryColor;
            decisionLineRenderer.startWidth = 0.04f;
            decisionLineRenderer.endWidth = 0.04f;
            decisionLineRenderer.useWorldSpace = true;
        }
    }

    void DrawAxes()
    {
        float yBot = -0.2f * graphScaleY;
        float yTop = 1.2f * graphScaleY;
        float xLeft = xMin * graphScaleX;
        float xRight = xMax * graphScaleX;

        if (xAxisRenderer != null)
        {
            xAxisRenderer.positionCount = 2;
            xAxisRenderer.startColor = axisColor;
            xAxisRenderer.endColor = axisColor;
            xAxisRenderer.startWidth = 0.03f;
            xAxisRenderer.endWidth = 0.03f;
            xAxisRenderer.useWorldSpace = true;
            xAxisRenderer.SetPosition(0, new Vector3(xLeft, 0f, 0f));
            xAxisRenderer.SetPosition(1, new Vector3(xRight, 0f, 0f));
        }

        if (yAxisRenderer != null)
        {
            yAxisRenderer.positionCount = 2;
            yAxisRenderer.startColor = axisColor;
            yAxisRenderer.endColor = axisColor;
            yAxisRenderer.startWidth = 0.03f;
            yAxisRenderer.endWidth = 0.03f;
            yAxisRenderer.useWorldSpace = true;
            yAxisRenderer.SetPosition(0, new Vector3(0f, yBot, 0f));
            yAxisRenderer.SetPosition(1, new Vector3(0f, yTop, 0f));
        }
    }

    // ── Sigmoid Curve ─────────────────────────────────────────
    void DrawSigmoidCurve()
    {
        curveRenderer.positionCount = resolution;
        for (int i = 0; i < resolution; i++)
        {
            float t = i / (float)(resolution - 1);
            float x = Mathf.Lerp(xMin, xMax, t);
            float y = Sigmoid(weight * x + bias);

            // Convert to world space
            float worldX = x * graphScaleX;
            float worldY = y * graphScaleY;

            curveRenderer.SetPosition(i, new Vector3(worldX, worldY, 0f));
        }
    }

    // ── Decision Boundary Line ────────────────────────────────
    void DrawDecisionBoundary()
    {
        if (decisionLineRenderer == null) return;
        if (Mathf.Abs(weight) < 0.001f) return;

        // Where sigmoid = 0.5 → w*x + b = 0 → x = -b/w
        float xBoundary = -bias / weight;
        float worldX = xBoundary * graphScaleX;

        decisionLineRenderer.positionCount = 2;
        decisionLineRenderer.SetPosition(0, new Vector3(worldX, -0.3f * graphScaleY, 0f));
        decisionLineRenderer.SetPosition(1, new Vector3(worldX,  1.3f * graphScaleY, 0f));
    }

    // ── UI Text Updates ───────────────────────────────────────
    void UpdateAllUIText()
    {
        if (formulaText != null)
            formulaText.text =
                $"f(x) = 1 / (1 + e^(-({weight:F2}·x + {bias:F2})))";

        if (weightValueText != null)
            weightValueText.text = $"Weight (w) = {weight:F2}";

        if (biasValueText != null)
            biasValueText.text = $"Bias (b) = {bias:F2}";

        if (boundaryValueText != null && Mathf.Abs(weight) > 0.01f)
            boundaryValueText.text =
                $"Decision Boundary: x = {(-bias / weight):F2}";

        if (probabilityText != null)
            probabilityText.text =
                $"P(y=1 | x=0) = {Sigmoid(bias):F3}";

        if (annotationText != null)
            annotationText.text = GetAnnotation();
    }

    string GetAnnotation()
    {
        if (Mathf.Abs(weight) < 0.2f)
            return "Weight ≈ 0: Curve is flat — model has NO confidence";
        else if (weight > 3f)
            return "High weight: Very steep — model is very confident";
        else if (weight < -0.3f)
            return "Negative weight: Feature negatively correlated";
        else if (Mathf.Abs(bias) > 2.5f)
            return "High bias: Decision boundary is shifted far";
        else
            return "Adjust the sliders to see the curve change!";
    }

    // ── Math ──────────────────────────────────────────────────
    public float Sigmoid(float z)
    {
        return 1f / (1f + Mathf.Exp(-z));
    }

    // Called externally by SliderController
    public void SetWeight(float w) { weight = w; }
    public void SetBias(float b)   { bias = b; }
}
