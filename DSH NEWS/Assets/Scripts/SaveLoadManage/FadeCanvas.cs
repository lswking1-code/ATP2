using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FadeCanvas : MonoBehaviour
{
    [Header("Event Listener")]
    public FadeEventSO fadeEvent;

    public Image fadeImage;

    private Coroutine fadeRoutine;

    private void OnEnable()
    {
        if (fadeEvent != null)
        {
            fadeEvent.OnEventRaised += OnFadeEvent;
        }
    }

    private void OnDisable()
    {
        if (fadeEvent != null)
        {
            fadeEvent.OnEventRaised -= OnFadeEvent;
        }
    }

    private void OnFadeEvent(Color target, float duration, bool fadeIn)
    {
        if (fadeImage == null)
        {
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeTo(target, Mathf.Max(0.01f, duration)));
    }

    private IEnumerator FadeTo(Color target, float duration)
    {
        var start = fadeImage.color;
        var elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            fadeImage.color = Color.Lerp(start, target, t);
            yield return null;
        }

        fadeImage.color = target;
        fadeRoutine = null;
    }
}
