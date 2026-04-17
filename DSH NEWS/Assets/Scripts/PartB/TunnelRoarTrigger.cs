using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TunnelRoarTrigger : MonoBehaviour
{
    [Header("Roar Audio")]
    [SerializeField, Tooltip("进入触发区后播放的嘶吼音效")]
    private AudioClip roarClip;

    [SerializeField, Tooltip("可选：3D 声源挂点。为空时使用当前触发器位置")]
    private Transform roarSourcePoint;

    [SerializeField, Range(0f, 1f), Tooltip("嘶吼音量（会再乘 AudioManager 的 SFX 总音量）")]
    private float volume = 1f;

    [SerializeField, Min(0f), Tooltip("3D 音频最小距离")]
    private float minDistance = 2f;

    [SerializeField, Min(0f), Tooltip("3D 音频最大距离")]
    private float maxDistance = 35f;

    [SerializeField, Tooltip("距离衰减模式")]
    private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    [Header("Trigger")]
    [SerializeField, Tooltip("是否只触发一次")]
    private bool triggerOnce = true;

    [SerializeField, Tooltip("运行时触发状态（只读）")]
    private bool hasTriggered = false;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (triggerOnce && hasTriggered)
        {
            return;
        }

        if (roarClip == null)
        {
            Debug.LogWarning("[TunnelRoarTrigger] roarClip is not assigned.", this);
            return;
        }

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            Debug.LogWarning("[TunnelRoarTrigger] AudioManager.Instance not found.", this);
            return;
        }

        hasTriggered = true;

        if (roarSourcePoint != null)
        {
            audioManager.PlaySFX3D(roarClip, roarSourcePoint, volume, minDistance, maxDistance, rolloffMode);
            return;
        }

        audioManager.PlaySFX3D(roarClip, transform.position, volume, minDistance, maxDistance, rolloffMode);
    }
}
