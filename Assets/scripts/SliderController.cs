using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class SliderController : MonoBehaviour
{
    public Slider weightSlider;
    public Slider biasSlider;
    public TextMeshProUGUI weightLabel;
    public TextMeshProUGUI biasLabel;
    public LogisticCurve logisticCurve;

    void Start()
    {
        weightSlider.minValue = -4f;
        weightSlider.maxValue = 4f;
        weightSlider.value = 1f;
        biasSlider.minValue = -4f;
        biasSlider.maxValue = 4f;
        biasSlider.value = 0f;
        weightSlider.onValueChanged.AddListener(OnWeightChanged);
        biasSlider.onValueChanged.AddListener(OnBiasChanged);

        // Remove default slider "Background" graphics that appear as a dark overlay block
        // in this scene due to large slider rects.
        HideSliderBackground(weightSlider);
        HideSliderBackground(biasSlider);
    }

    void HideSliderBackground(Slider slider)
    {
        if (slider == null) return;
        // Disable ANY child named "Background" (some slider hierarchies differ).
        foreach (var t in slider.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            if (!string.Equals(t.name, "Background", StringComparison.OrdinalIgnoreCase)) continue;
            var img = t.GetComponent<Image>();
            if (img != null) img.enabled = false;
            t.gameObject.SetActive(false);
        }
    }

    void OnWeightChanged(float val)
    {
        if (logisticCurve != null) logisticCurve.SetWeight(val);
        if (weightLabel != null) weightLabel.text = "w = " + val.ToString("F2");
    }

    void OnBiasChanged(float val)
    {
        if (logisticCurve != null) logisticCurve.SetBias(val);
        if (biasLabel != null) biasLabel.text = "b = " + val.ToString("F2");
    }

    public IEnumerator AnimateWeightTo(float target, float duration)
    {
        float start = weightSlider.value;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            weightSlider.value = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        weightSlider.value = target;
    }

    public IEnumerator AnimateBiasTo(float target, float duration)
    {
        float start = biasSlider.value;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            biasSlider.value = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        biasSlider.value = target;
    }
}