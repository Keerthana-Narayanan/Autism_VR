using UnityEngine;
using UnityEngine.UI;
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

    private bool isPaused = false;
    private bool isInBreak = false;
    private float _initialNarrationVolume = -1f;
    private float _initialMusicVolume = -1f;

    public bool IsInBreak => isInBreak;

    void Start()
    {
        if (narrationSource != null) _initialNarrationVolume = narrationSource.volume;
        if (musicSource != null) _initialMusicVolume = musicSource.volume;

        if (backgroundMusic != null && musicSource != null)
        {
            musicSource.clip = backgroundMusic;
            musicSource.loop = true;
            // Respect whatever the scene had unless it's unset.
            if (_initialMusicVolume < 0f) _initialMusicVolume = 0.08f;
            musicSource.volume = _initialMusicVolume;
            musicSource.Play();
        }

        StartCoroutine(RunFullLesson());
    }

    // AI PANEL PAUSE
    public void PauseLesson()
    {
        isPaused = true;
        isInBreak = false;

        if (narrationSource != null)
            narrationSource.Pause();

        if (musicSource != null)
            musicSource.volume = (_initialMusicVolume > 0f) ? _initialMusicVolume * 0.35f : 0.03f;

        if (subtitleText != null)
            subtitleText.text = "Lesson paused. Ask your doubt!";
    }

    // AI PANEL RESUME
    public void ResumeLesson()
    {
        isPaused = false;
        isInBreak = false;

        if (narrationSource != null)
        {
            if (_initialNarrationVolume > 0f) narrationSource.volume = _initialNarrationVolume;
            narrationSource.UnPause();
        }

        if (musicSource != null)
            musicSource.volume = (_initialMusicVolume > 0f) ? _initialMusicVolume : 0.08f;

        if (subtitleText != null)
            subtitleText.text = "Lesson resumed. Welcome back!";
    }

    IEnumerator WaitWhilePaused()
    {
        while (isPaused)
            yield return null;
    }

    public void BreakLesson()
    {
        isPaused = true;
        isInBreak = true;

        if (narrationSource != null)
            narrationSource.Pause();

        if (musicSource != null)
            musicSource.volume = (_initialMusicVolume > 0f) ? _initialMusicVolume * 0.15f : 0.01f;

        if (subtitleText != null)
            subtitleText.text = "Drowsiness detected. Taking a short break...";
    }

    IEnumerator WaitForSecondsWithPause(float seconds)
    {
        // Waits for lesson time, respecting PauseLesson/BreakLesson.
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (!isPaused)
                elapsed += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator RunFullLesson()
    {
        // INTRO
        SetSubtitle("Welcome to Logistic Regression!");
        yield return StartCoroutine(PlayAndWait(clip_Intro));

        yield return StartCoroutine(WaitForSecondsWithPause(1.5f));

        // SIGMOID
        SetSubtitle("This S-shaped curve is called the sigmoid function.");
        yield return StartCoroutine(PlayAndWait(clip_SigmoidExplain));

        yield return StartCoroutine(WaitForSecondsWithPause(1f));

        // =============================
        // WEIGHT DEMO (125 seconds)
        // =============================

        narrationSource.clip = clip_WeightDemo;
        narrationSource.Play();

        SetSubtitle("Watch the weight slider.");
        yield return StartCoroutine(WaitForSecondsWithPause(8f));

        SetSubtitle("Did you see that?");
        yield return StartCoroutine(WaitForSecondsWithPause(12f)); // 20 - 8

        SetSubtitle("Now watch when we bring the weight close to zero.");
        yield return StartCoroutine(sliderController.AnimateWeightTo(0.1f, 4f));
        yield return StartCoroutine(WaitForSecondsWithPause(23f)); // 47 - 24

        SetSubtitle("Look at that flat curve.");
        yield return StartCoroutine(WaitForSecondsWithPause(6f)); // 53 - 47

        SetSubtitle("Now make the weight negative.");
        yield return StartCoroutine(sliderController.AnimateWeightTo(-2.5f, 4f));
        yield return StartCoroutine(WaitForSecondsWithPause(33f)); // 86 - 53

        SetSubtitle("Resetting the weight back to normal.");
        yield return StartCoroutine(sliderController.AnimateWeightTo(1.5f, 3f));
        yield return StartCoroutine(WaitForSecondsWithPause(36f)); // 122 - 86

        // =============================
        // BIAS DEMO (111 seconds)
        // =============================

        narrationSource.clip = clip_BiasDemo;
        narrationSource.Play();

        SetSubtitle("Move the bias slider.");
        yield return StartCoroutine(WaitForSecondsWithPause(19f));

        SetSubtitle("Did you notice the boundary sliding?");
        yield return StartCoroutine(WaitForSecondsWithPause(6f)); // 25 - 19

        SetSubtitle("When bias becomes positive.");
        yield return StartCoroutine(sliderController.AnimateBiasTo(2.5f, 3f));
        yield return StartCoroutine(WaitForSecondsWithPause(29f)); // 54 - 25

        SetSubtitle("When bias becomes negative.");
        yield return StartCoroutine(sliderController.AnimateBiasTo(-2.5f, 3f));
        yield return StartCoroutine(WaitForSecondsWithPause(4f)); // 58 - 54

        SetSubtitle("Resetting bias back to zero.");
        yield return StartCoroutine(sliderController.AnimateBiasTo(0f, 3f));
        yield return StartCoroutine(WaitForSecondsWithPause(50f)); // 108 - 58

        // =============================
        // DATA POINT DEMO (104 seconds)
        // =============================

        narrationSource.clip = clip_DataPoints;
        narrationSource.Play();

        SetSubtitle("New data points appear.");
        yield return StartCoroutine(WaitForSecondsWithPause(9f));

        SetSubtitle("Red dots represent class zero.");
        yield return StartCoroutine(WaitForSecondsWithPause(4f)); // 13 - 9

        SetSubtitle("Blue dots represent class one.");
        yield return StartCoroutine(dataPointManager.SpawnAllDotsAnimated());
        yield return StartCoroutine(WaitForSecondsWithPause(13f)); // 26 - 13

        SetSubtitle("The decision boundary sits between the two groups.");
        yield return StartCoroutine(WaitForSecondsWithPause(15f)); // 41 - 26

        // =============================
        // SUMMARY
        // =============================

        SetSubtitle("Let us recap everything we learned today.");
        yield return StartCoroutine(PlayAndWait(clip_Summary));

        SetSubtitle("See you in the next lesson!");
        yield return StartCoroutine(FadeOutMusic(3f));

        SetSubtitle("");
    }

    IEnumerator PlayAndWait(AudioClip clip)
    {
        if (clip == null) yield break;

        narrationSource.clip = clip;
        narrationSource.Play();

        float elapsed = 0f;

        while (elapsed < clip.length)
        {
            if (!isPaused)
                elapsed += Time.deltaTime;

            yield return null;
        }
    }

    void SetSubtitle(string message)
    {
        if (subtitleText != null)
            subtitleText.text = message;
    }

    IEnumerator FadeOutMusic(float duration)
    {
        float startVolume = musicSource.volume;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, time / duration);
            yield return null;
        }

        musicSource.Stop();
    }
}