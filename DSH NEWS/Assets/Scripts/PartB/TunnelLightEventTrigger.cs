using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TunnelLightEventTrigger : MonoBehaviour
{
    [Header("Lights")]
    [SerializeField, Tooltip("隧道内受影响的灯光列表")]
    private List<Light> tunnelLights = new List<Light>();

    [Header("Blink")]
    [SerializeField, Min(0f), Tooltip("闪烁持续时长（秒）")]
    private float blinkDuration = 5f;

    [SerializeField, Min(0.01f), Tooltip("两次闪烁切换最短间隔（秒）")]
    private float blinkIntervalMin = 0.06f;

    [SerializeField, Min(0.01f), Tooltip("两次闪烁切换最长间隔（秒）")]
    private float blinkIntervalMax = 0.2f;

    [SerializeField, Range(0f, 1f), Tooltip("每次刷新时关灯概率")]
    private float offProbability = 0.65f;

    [Header("Blackout and Ambient")]
    [SerializeField, Min(0f), Tooltip("全黑后环境光插值到目标值的时长（秒），0 为瞬间")]
    private float blackoutFadeDuration = 0.5f;

    [SerializeField, Min(0f), Tooltip("事件触发后环境光强度目标值（整体压暗）")]
    private float targetAmbientIntensity = 0.08f;

    [Header("Emergency")]
    [SerializeField, Tooltip("应急阶段要点亮的红色灯（场景中默认禁用 Light）")]
    private List<Light> emergencyLights = new List<Light>();

    [SerializeField, Min(0f), Tooltip("全黑后等待多久再点亮应急灯（秒）")]
    private float emergencyDelaySeconds = 3f;

    [SerializeField, Tooltip("应急灯亮起时播放的一次性音效（可选）")]
    private AudioClip emergencyClip;

    [SerializeField, Tooltip("应急音效的 3D 声源点；为空则用当前触发器位置")]
    private Transform emergencySfxPoint;

    [SerializeField, Range(0f, 1f)]
    private float emergencySfxVolume = 0.85f;

    [SerializeField, Min(0f)]
    private float emergencySfxMinDistance = 2f;

    [SerializeField, Min(0f)]
    private float emergencySfxMaxDistance = 30f;

    [Header("Loop Event Audio")]
    [SerializeField, Tooltip("闪烁期间循环播放的音效")]
    private AudioClip loopSfx;

    [SerializeField, Tooltip("可选：循环音效的 3D 声源点；为空时使用当前触发器位置")]
    private Transform sfxSourcePoint;

    [SerializeField, Range(0f, 1f), Tooltip("循环音效音量（会再乘 AudioManager 的 SFX 总音量）")]
    private float volume = 0.7f;

    [SerializeField, Min(0f)]
    private float minDistance = 2f;

    [SerializeField, Min(0f)]
    private float maxDistance = 25f;

    [SerializeField]
    private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    [SerializeField, Min(0f), Tooltip("事件结束停止循环音效时的淡出时长（秒）")]
    private float audioFadeOut = 0.2f;

    [Header("Trigger")]
    [SerializeField, Tooltip("是否只触发一次")]
    private bool triggerOnce = true;

    [SerializeField, Tooltip("运行时触发状态（只读）")]
    private bool hasTriggered = false;

    private bool isRunning = false;
    private bool emergencySequenceCompleted = false;
    private AudioSource playingLoopSource;
    private readonly List<LightSnapshot> snapshots = new List<LightSnapshot>();

    private float ambientIntensityBefore;
    private Coroutine ambientFadeCoroutine;

    private struct LightSnapshot
    {
        public Light light;
        public bool enabled;
        public float intensity;
    }

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnDisable()
    {
        StopLoopAudio();
        if (ambientFadeCoroutine != null)
        {
            StopCoroutine(ambientFadeCoroutine);
            ambientFadeCoroutine = null;
        }

        if (!emergencySequenceCompleted)
        {
            RestoreLights();
            RestoreAmbientImmediate();
        }

        isRunning = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (isRunning) return;
        if (triggerOnce && hasTriggered) return;

        hasTriggered = true;
        StartCoroutine(RunEvent());
    }

    private IEnumerator RunEvent()
    {
        isRunning = true;
        emergencySequenceCompleted = false;
        ambientIntensityBefore = RenderSettings.ambientIntensity;

        CaptureInitialLights();
        StartLoopAudio();

        float elapsed = 0f;
        while (elapsed < blinkDuration)
        {
            ApplyBlinkStep();

            float interval = Random.Range(blinkIntervalMin, blinkIntervalMax);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        StopLoopAudio();

        BlackoutTunnelLights();

        if (blackoutFadeDuration <= 0f)
        {
            RenderSettings.ambientIntensity = targetAmbientIntensity;
        }
        else
        {
            if (ambientFadeCoroutine != null)
            {
                StopCoroutine(ambientFadeCoroutine);
            }

            ambientFadeCoroutine = StartCoroutine(FadeAmbientRoutine(ambientIntensityBefore, targetAmbientIntensity, blackoutFadeDuration));
            yield return ambientFadeCoroutine;
            ambientFadeCoroutine = null;
        }

        yield return new WaitForSeconds(emergencyDelaySeconds);

        EnableEmergencyLights();
        PlayEmergencySfxOnce();

        emergencySequenceCompleted = true;
        isRunning = false;
    }

    private void BlackoutTunnelLights()
    {
        for (int i = 0; i < snapshots.Count; i++)
        {
            LightSnapshot snapshot = snapshots[i];
            if (snapshot.light == null) continue;
            snapshot.light.enabled = false;
        }
    }

    private IEnumerator FadeAmbientRoutine(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            RenderSettings.ambientIntensity = Mathf.Lerp(from, to, k);
            yield return null;
        }

        RenderSettings.ambientIntensity = to;
    }

    private void RestoreAmbientImmediate()
    {
        RenderSettings.ambientIntensity = ambientIntensityBefore;
    }

    private void EnableEmergencyLights()
    {
        for (int i = 0; i < emergencyLights.Count; i++)
        {
            Light light = emergencyLights[i];
            if (light == null) continue;
            light.enabled = true;
        }
    }

    private void PlayEmergencySfxOnce()
    {
        if (emergencyClip == null) return;

        AudioManager manager = AudioManager.Instance;
        if (manager == null) return;

        Transform anchor = emergencySfxPoint != null ? emergencySfxPoint : transform;
        manager.PlaySFX3D(
            emergencyClip,
            anchor,
            emergencySfxVolume,
            emergencySfxMinDistance,
            emergencySfxMaxDistance,
            rolloffMode);
    }

    private void CaptureInitialLights()
    {
        snapshots.Clear();
        for (int i = 0; i < tunnelLights.Count; i++)
        {
            Light light = tunnelLights[i];
            if (light == null) continue;

            snapshots.Add(new LightSnapshot
            {
                light = light,
                enabled = light.enabled,
                intensity = light.intensity
            });
        }
    }

    private void ApplyBlinkStep()
    {
        for (int i = 0; i < snapshots.Count; i++)
        {
            LightSnapshot snapshot = snapshots[i];
            if (snapshot.light == null) continue;

            bool shouldTurnOff = Random.value < offProbability;
            if (shouldTurnOff)
            {
                snapshot.light.enabled = false;
                continue;
            }

            snapshot.light.enabled = true;
            snapshot.light.intensity = snapshot.intensity;
        }
    }

    private void RestoreLights()
    {
        for (int i = 0; i < snapshots.Count; i++)
        {
            LightSnapshot snapshot = snapshots[i];
            if (snapshot.light == null) continue;
            snapshot.light.enabled = snapshot.enabled;
            snapshot.light.intensity = snapshot.intensity;
        }
    }

    private void StartLoopAudio()
    {
        AudioManager manager = AudioManager.Instance;
        if (manager == null || loopSfx == null) return;

        Transform anchor = sfxSourcePoint != null ? sfxSourcePoint : transform;
        playingLoopSource = manager.PlayLoopSFX3D(
            loopSfx,
            anchor,
            volume,
            minDistance,
            maxDistance,
            rolloffMode);
    }

    private void StopLoopAudio()
    {
        if (playingLoopSource == null) return;

        AudioManager manager = AudioManager.Instance;
        if (manager != null)
        {
            manager.StopLoopSFX(playingLoopSource, audioFadeOut);
        }

        playingLoopSource = null;
    }
}
