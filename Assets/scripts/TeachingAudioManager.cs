using UnityEngine;
using System.Collections;
using TMPro;

public class TeachingAudioManager : MonoBehaviour
{
    [Header("Narration Clips")]
    public AudioClip clip_Intro;
    public AudioClip clip_SigmoidExplain;
    public AudioClip clip_WeightDemo;
    public AudioClip clip_BiasDemo;
    public AudioClip clip_DataPoints;
    public AudioClip clip_Summary;

    [Header("Background Music")]
    public AudioClip backgroundMusic;

    [Header("Audio Sources")]
    public AudioSource narrationSource;
    public AudioSource musicSource;

    [Header("References")]
    public SliderController sliderController;
    public DataPointManager dataPointManager;
    public LogisticCurve logisticCurve;

    [Header("Subtitle Text")]
    public TextMeshProUGUI subtitleText;

    void Start()
    {
        // Start background music quietly
        if (backgroundMusic != null && musicSource != null)
        {
            musicSource.clip = backgroundMusic;
            musicSource.loop = true;
            musicSource.volume = 0.08f;
            musicSource.Play();
        }

        // Start the full lesson
        StartCoroutine(RunFullLesson());
    }

    IEnumerator RunFullLesson()
    {
        // ══════════════════════════════════════
        // SEGMENT 1 — INTRODUCTION
        // Curve stays still, just audio plays
        // ══════════════════════════════════════
        SetSubtitle("Welcome to Logistic Regression!");
        yield return StartCoroutine(PlayAndWait(clip_Intro));
        yield return new WaitForSeconds(1.5f);

        // ══════════════════════════════════════
        // SEGMENT 2 — SIGMOID EXPLANATION
        // Curve stays still, audio explains it
        // ══════════════════════════════════════
        SetSubtitle("This S-shaped curve is the Sigmoid function...");
        yield return StartCoroutine(PlayAndWait(clip_SigmoidExplain));
        yield return new WaitForSeconds(1f);

        // ══════════════════════════════════════
        // SEGMENT 3 — WEIGHT DEMO
        // Slider moves automatically with audio
        // ══════════════════════════════════════
        SetSubtitle("Watch what happens when we change the Weight...");

        // Start playing audio
        narrationSource.clip = clip_WeightDemo;
        narrationSource.Play();

        // Wait 4 seconds — audio says
        // "Watch the weight slider, increasing slowly"
        yield return new WaitForSeconds(4f);

        // ANIMATE weight UP — curve gets steeper
        // Audio is saying "See how the curve gets steeper"
        SetSubtitle("Higher weight = Steeper curve = More confident!");
        yield return StartCoroutine(
            sliderController.AnimateWeightTo(3.5f, 4f));

        // Wait 3 seconds — audio explains steep curve
        yield return new WaitForSeconds(3f);

        // ANIMATE weight to ZERO — curve goes flat
        // Audio is saying "watch weight close to zero"
        SetSubtitle("Weight near Zero = Flat curve = No confidence!");
        yield return StartCoroutine(
            sliderController.AnimateWeightTo(0.1f, 3f));

        // Wait 3 seconds — audio explains flat curve
        yield return new WaitForSeconds(3f);

        // ANIMATE weight NEGATIVE — curve flips
        // Audio is saying "watch when weight goes negative"
        SetSubtitle("Negative weight = Flipped curve!");
        yield return StartCoroutine(
            sliderController.AnimateWeightTo(-2.5f, 3f));

        // Wait 3 seconds — audio explains flipped curve
        yield return new WaitForSeconds(3f);

        // Reset weight back to normal
        SetSubtitle("Resetting weight back to normal...");
        yield return StartCoroutine(
            sliderController.AnimateWeightTo(1.5f, 2f));

        // Wait for audio to finish
        yield return new WaitForSeconds(2f);

        // ══════════════════════════════════════
        // SEGMENT 4 — BIAS DEMO
        // Boundary line moves with audio
        // ══════════════════════════════════════
        SetSubtitle("Now let us understand the Bias parameter...");

        // Start playing audio
        narrationSource.clip = clip_BiasDemo;
        narrationSource.Play();

        // Wait 4 seconds — audio introduces bias
        yield return new WaitForSeconds(4f);

        // ANIMATE bias RIGHT — boundary moves right
        // Audio saying "watch the curve slide"
        SetSubtitle("Bias shifts the Decision Boundary left and right!");
        yield return StartCoroutine(
            sliderController.AnimateBiasTo(2.5f, 4f));

        // Wait 3 seconds — audio explains
        yield return new WaitForSeconds(3f);

        // ANIMATE bias LEFT
        SetSubtitle("Negative bias moves boundary to the right...");
        yield return StartCoroutine(
            sliderController.AnimateBiasTo(-2.5f, 4f));

        // Wait 3 seconds
        yield return new WaitForSeconds(3f);

        // Reset bias to center
        SetSubtitle("Resetting bias back to zero...");
        yield return StartCoroutine(
            sliderController.AnimateBiasTo(0f, 2f));

        // Wait for audio to finish
        yield return new WaitForSeconds(2f);

        // ══════════════════════════════════════
        // SEGMENT 5 — DATA POINTS
        // Dots appear one by one with audio
        // ══════════════════════════════════════
        SetSubtitle("Now let us add some training data...");

        // Start playing audio
        narrationSource.clip = clip_DataPoints;
        narrationSource.Play();

        // Wait 3 seconds — audio introduces data
        yield return new WaitForSeconds(3f);

        // Spawn dots one by one
        SetSubtitle("Red dots = Class 0    Blue dots = Class 1");
        yield return StartCoroutine(
            dataPointManager.SpawnAllDotsAnimated());

        // Wait for audio to finish
        yield return new WaitForSeconds(4f);

        // Show how boundary sits between clusters
        SetSubtitle("The boundary sits between the two groups!");
        yield return new WaitForSeconds(3f);

        // ══════════════════════════════════════
        // SEGMENT 6 — SUMMARY
        // All elements visible, audio recaps
        // ══════════════════════════════════════
        SetSubtitle("Let us recap everything we learned today...");
        yield return StartCoroutine(PlayAndWait(clip_Summary));

        // Fade out music
        SetSubtitle("See you in the next lesson!");
        yield return StartCoroutine(FadeOutMusic(3f));

        yield return new WaitForSeconds(2f);
        SetSubtitle("");
    }

    // ── Play audio and wait for it to finish ──
    IEnumerator PlayAndWait(AudioClip clip)
    {
        if (clip == null) yield break;
        narrationSource.clip = clip;
        narrationSource.Play();
        yield return new WaitForSeconds(clip.length);
    }

    // ── Update subtitle text ──
    void SetSubtitle(string message)
    {
        if (subtitleText != null)
            subtitleText.text = message;
    }

    // ── Fade out background music ──
    IEnumerator FadeOutMusic(float duration)
    {
        if (musicSource == null) yield break;
        float startVolume = musicSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(
                startVolume, 0f, elapsed / duration);
            yield return null;
        }
        musicSource.Stop();
    }
}