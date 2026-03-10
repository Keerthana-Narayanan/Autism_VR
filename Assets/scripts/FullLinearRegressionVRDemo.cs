using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FullLinearRegressionVRDemo : MonoBehaviour
{
    // Adjustable parameters
    public int numPoints = 60;
    public float xRange = 10f;
    public float trueSlope = 0.5f;
    public float trueIntercept = 1.0f;
    public float noiseStd = 1.2f;
    public float pointScale = 0.18f;

    private List<GameObject> points = new List<GameObject>();
    private List<LineRenderer> residualLines = new List<LineRenderer>();
    private LineRenderer regressionLine;
    private Camera mainCam;

    private GameObject canvasGO;
    private Text sseText;
    private Text formulaText;
    private Text captionText;

    private float[] xs;
    private float[] ys;

    private float optSlope;
    private float optIntercept;

    private float animDuration = 40f;
    private float startSlope, startIntercept;

    void Awake()
    {
        mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camGO = new GameObject("MainCamera");
            mainCam = camGO.AddComponent<Camera>();
            camGO.tag = "MainCamera";
            mainCam.transform.position = new Vector3(5, 4, -10);
            mainCam.transform.LookAt(Vector3.zero);
        }
    }

    void Start()
    {
        CreateFloorAndAxes();
        GenerateData();
        CreatePointObjects();
        ComputeOLS();
        CreateRegressionLine();
        CreateWorldSpaceUI();

        System.Random r = new System.Random();
        startSlope = (float)(optSlope + (r.NextDouble() - 0.5) * 4.0);
        startIntercept = (float)(optIntercept + (r.NextDouble() - 0.5) * 6.0);

        StartCoroutine(RunDemo());
    }

    void CreateFloorAndAxes()
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.transform.localScale = new Vector3(2, 1, 2);
        plane.transform.position = new Vector3(xRange / 2f, -1f, 0);

        // X axis (white)
        CreateThinLine(new Vector3(0, 0, 0), new Vector3(xRange + 1f, 0, 0), Color.white, "X-Axis");
        // Y axis (white)
        CreateThinLine(new Vector3(0, 0, 0), new Vector3(0, 6f, 0), Color.white, "Y-Axis");
    }

    void GenerateData()
    {
        xs = new float[numPoints];
        ys = new float[numPoints];
        System.Random rnd = new System.Random();
        for (int i = 0; i < numPoints; i++)
        {
            float x = (float)i / (numPoints - 1) * xRange;
            x += (float)(rnd.NextDouble() - 0.5) * (xRange / numPoints) * 0.6f;
            float noise = (float)Gaussian(rnd, 0f, noiseStd);
            float y = trueSlope * x + trueIntercept + noise;
            xs[i] = x;
            ys[i] = y;
        }
    }

    void CreatePointObjects()
    {
        for (int i = 0; i < numPoints; i++)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.transform.position = new Vector3(xs[i], ys[i], 0f);
            s.transform.localScale = Vector3.one * pointScale;
            var r = s.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Standard"));
            r.material.color = Color.cyan;
            points.Add(s);

            // residual line for each point
            GameObject rl = new GameObject("residual_" + i);
            LineRenderer l = rl.AddComponent<LineRenderer>();
            l.positionCount = 2;
            l.startWidth = 0.03f;
            l.endWidth = 0.03f;
            l.material = new Material(Shader.Find("Sprites/Default"));
            l.startColor = Color.red;
            l.endColor = Color.red;
            residualLines.Add(l);
        }
    }

    void CreateRegressionLine()
    {
        GameObject go = new GameObject("RegressionLine");
        regressionLine = go.AddComponent<LineRenderer>();
        regressionLine.positionCount = 2;
        regressionLine.startWidth = 0.06f;
        regressionLine.endWidth = 0.06f;
        regressionLine.material = new Material(Shader.Find("Sprites/Default"));
        regressionLine.startColor = Color.blue;
        regressionLine.endColor = Color.blue;
    }

    void CreateWorldSpaceUI()
    {
        canvasGO = new GameObject("WorldCanvas");
        Canvas c = canvasGO.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();
        RectTransform rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(600, 300);
        canvasGO.transform.position = new Vector3(xRange / 2f, 5.2f, -1.5f);
        canvasGO.transform.rotation = Quaternion.Euler(10, 0, 0);
        canvasGO.transform.localScale = Vector3.one * 0.02f;

        // SSE Text
        GameObject sseGO = new GameObject("SSE");
        sseGO.transform.SetParent(canvasGO.transform, false);
        sseText = sseGO.AddComponent<Text>();
        sseText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        sseText.fontSize = 28;
        sseText.alignment = TextAnchor.UpperLeft;
        sseText.rectTransform.anchoredPosition = new Vector2(-250, 90);
        sseText.text = "SSE: ...";

        // Formula Text
        GameObject fGO = new GameObject("Formula");
        fGO.transform.SetParent(canvasGO.transform, false);
        formulaText = fGO.AddComponent<Text>();
        formulaText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        formulaText.fontSize = 22;
        formulaText.alignment = TextAnchor.UpperLeft;
        formulaText.rectTransform.anchoredPosition = new Vector2(-250, 40);
        formulaText.text = "y = m x + b";

        // Caption
        GameObject cGO = new GameObject("Caption");
        cGO.transform.SetParent(canvasGO.transform, false);
        captionText = cGO.AddComponent<Text>();
        captionText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        captionText.fontSize = 20;
        captionText.alignment = TextAnchor.UpperLeft;
        captionText.rectTransform.anchoredPosition = new Vector2(-250, -10);
        captionText.text = "Residuals: shortest distance to the line";
    }

    IEnumerator RunDemo()
    {
        float t = 0f;
        while (t < animDuration)
        {
            float frac = t / animDuration;
            float e = Mathf.SmoothStep(0f, 1f, frac);
            float curSlope = Mathf.Lerp(startSlope, optSlope, e);
            float curIntercept = Mathf.Lerp(startIntercept, optIntercept, e);

            // regression line
            Vector3 pA = new Vector3(-1f, curSlope * -1f + curIntercept, 0f);
            Vector3 pB = new Vector3(xRange + 1f, curSlope * (xRange + 1f) + curIntercept, 0f);
            regressionLine.SetPosition(0, pA);
            regressionLine.SetPosition(1, pB);

            float sse = 0f;
            for (int i = 0; i < numPoints; i++)
            {
                float x = xs[i];
                float y = ys[i];

                // shortest distance projection onto line
                Vector3 point = new Vector3(x, y, 0f);
                Vector3 lineDir = (pB - pA).normalized;
                Vector3 proj = pA + Vector3.Dot(point - pA, lineDir) * lineDir;

                float resid = (point - proj).magnitude;
                sse += resid * resid;

                residualLines[i].SetPosition(0, point);
                residualLines[i].SetPosition(1, proj);

                float mag = Mathf.Clamp01(resid / (noiseStd * 3f));
                var rend = points[i].GetComponent<Renderer>();
                rend.material.color = Color.Lerp(Color.cyan, Color.magenta, mag);
            }

            sseText.text = $"SSE: {sse:F2}";
            formulaText.text = $"y = {curSlope:F3} x + {curIntercept:F3}";
            captionText.text = $"Converging to best fit — {Mathf.Round(frac * 100)}%";

            CameraOrbitAround(Time.deltaTime);

            t += Time.deltaTime;
            yield return null;
        }

        captionText.text = "Done — regression line minimizes SSE";
        yield break;
    }

    void CameraOrbitAround(float delta)
    {
        if (mainCam == null) return;
        mainCam.transform.RotateAround(new Vector3(xRange / 2f, 1.5f, 0f), Vector3.up, 8f * delta);
        mainCam.transform.LookAt(new Vector3(xRange / 2f, 2f, 0f));
    }

    void ComputeOLS()
    {
        int n = numPoints;
        double sumX = 0, sumY = 0, sumXX = 0, sumXY = 0;
        for (int i = 0; i < n; i++)
        {
            sumX += xs[i];
            sumY += ys[i];
            sumXX += xs[i] * xs[i];
            sumXY += xs[i] * ys[i];
        }
        double denom = n * sumXX - sumX * sumX;
        if (Mathf.Approximately((float)denom, 0f))
        {
            optSlope = 0f;
            optIntercept = 0f;
        }
        else
        {
            optSlope = (float)((n * sumXY - sumX * sumY) / denom);
            optIntercept = (float)((sumY - optSlope * sumX) / n);
        }
    }

    void CreateThinLine(Vector3 a, Vector3 b, Color c, string name)
    {
        GameObject g = new GameObject(name);
        var lr = g.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startWidth = 0.04f;
        lr.endWidth = 0.04f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = c;
        lr.endColor = c;
    }

    double Gaussian(System.Random r, double mu, double sigma)
    {
        double u1 = 1.0 - r.NextDouble();
        double u2 = 1.0 - r.NextDouble();
        double randStdNormal = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) *
                               System.Math.Sin(2.0 * System.Math.PI * u2);
        return mu + sigma * randStdNormal;
    }
}
