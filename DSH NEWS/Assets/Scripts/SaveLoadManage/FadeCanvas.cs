using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FadeCanvas : MonoBehaviour
{
    [Header("Event Listener")]
    public FadeEventSO fadeEvent;

    public Image fadeImage;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        SyncFadeRaycastBlocking();
    }

    /// <summary>
    /// 全屏 fade 在 alpha≈0 时仍默认 raycastTarget=true 会挡住整界面点击（与 glitch 视频层无关）。
    /// 仅在需要遮罩（渐显中有不透明度）时参与射线检测。
    /// </summary>
    private void SyncFadeRaycastBlocking()
    {
        if (fadeImage == null)
            return;
        const float threshold = 1f / 255f;
        fadeImage.raycastTarget = fadeImage.color.a > threshold;
    }

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
            SyncFadeRaycastBlocking();
            yield return null;
        }

        fadeImage.color = target;
        SyncFadeRaycastBlocking();
        fadeRoutine = null;
    }
}
