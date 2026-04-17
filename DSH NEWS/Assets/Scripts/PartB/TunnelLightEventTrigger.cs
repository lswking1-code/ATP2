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
    private AudioSource playingLoopSource;
    private readonly List<LightSnapshot> snapshots = new List<LightSnapshot>();

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
        RestoreLights();
        StopLoopAudio();
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

        RestoreLights();
        StopLoopAudio();
        isRunning = false;
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
