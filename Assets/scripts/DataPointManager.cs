using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DataPointManager : MonoBehaviour
{
    [Header("Dot Prefab")]
    public GameObject dotPrefab;

    [Header("Graph Parent")]
    public Transform graphParent;

    [Header("Graph Scale")]
    public float graphScaleX = 1.0f;
    public float graphScaleY = 2.0f;

    public float dotSize = 0.25f;

    float[] class0_xValues = { -4.5f, -3.8f, -3.2f, -2.6f, -2.0f, -1.5f };
    float[] class1_xValues = {  1.5f,  2.0f,  2.7f,  3.2f,  3.8f,  4.5f };

    Color class0Color = new Color(1.0f, 0.3f, 0.3f, 1f);
    Color class1Color = new Color(0.3f, 0.6f, 1.0f, 1f);

    private List<GameObject> spawnedDots = new List<GameObject>();

    public IEnumerator SpawnAllDotsAnimated()
    {
        foreach (float x in class0_xValues)
        {
            SpawnDot(x, 0);
            yield return new WaitForSeconds(0.25f);
        }
        yield return new WaitForSeconds(0.5f);
        foreach (float x in class1_xValues)
        {
            SpawnDot(x, 1);
            yield return new WaitForSeconds(0.25f);
        }
    }

    void SpawnDot(float xValue, int label)
    {
        float yGraph = (label == 1) ? 0.92f : 0.08f;
        float worldX = xValue * graphScaleX;
        float worldY = yGraph  * graphScaleY;

        GameObject dot = Instantiate(dotPrefab,
            new Vector3(worldX, worldY, 0f),
            Quaternion.identity, graphParent);

        SpriteRenderer sr = dot.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = (label == 1) ? class1Color : class0Color;

        dot.transform.localScale = Vector3.zero;
        StartCoroutine(ScaleIn(dot.transform, dotSize, 0.2f));
        spawnedDots.Add(dot);
    }

    IEnumerator ScaleIn(Transform t, float targetScale, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float scale = Mathf.SmoothStep(0f, targetScale, elapsed / duration);
            t.localScale = Vector3.one * scale;
            yield return null;
        }
        t.localScale = Vector3.one * targetScale;
    }
}