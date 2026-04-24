using System.Collections;
using UnityEngine;

/// <summary>
/// 厕所事件总控：
/// 1) 玩家经过厕所周围触发区 -> 播放厕所内声音
/// 2) 玩家进入厕所 -> 强制关门并锁交互
/// 3) 关门后灯光闪烁并播放室内声
/// 4) 随后播放门外怪物叫声
/// 5) 怪物叫声结束 -> 解锁门交互并停止闪烁
/// </summary>
public class ToiletEventController : MonoBehaviour
{
    [System.Serializable]
    private struct Sound3DSettings
    {
        [Range(0f, 1f)] public float volume;
        [Min(0f)] public float minDistance;
        [Min(0f)] public float maxDistance;
        public AudioRolloffMode rolloffMode;

        public void Clamp()
        {
            if (maxDistance < minDistance) maxDistance = minDistance;
        }
    }

    [Header("Activation (ValueManage)")]
    [SerializeField, Tooltip(">=0 时要求 day 精确匹配；-1 表示不检查 day。")]
    private int targetDay = -1;

    [SerializeField, Tooltip("非空时要求 GetSituation(targetSituationId) 为 true（大小写敏感）。")]
    private string targetSituationId = "";

    [SerializeField, Tooltip("留空则自动查找场景中的 ValueManage。")]
    private ValueManage valueManage;

    [Header("Door")]
    [SerializeField, Tooltip("厕所门控制器")]
    private DoorController doorController;

    [SerializeField, Tooltip("进入厕所后是否强制关门")]
    private bool closeDoorOnEnter = true;

    [Header("Toilet Nearby Sound (Step 1)")]
    [SerializeField, Tooltip("玩家经过厕所周围时，从厕所内传出的声音")]
    private AudioClip nearAreaToiletClip;

    [SerializeField, Tooltip("厕所内声音 3D 声源点（为空时使用当前对象）")]
    private Transform toiletInnerAudioPoint;

    [SerializeField, Tooltip("Step1 声音参数（音量/范围/衰减）")]
    private Sound3DSettings nearAreaSoundSettings = new Sound3DSettings
    {
        volume = 1f,
        minDistance = 2f,
        maxDistance = 25f,
        rolloffMode = AudioRolloffMode.Logarithmic
    };

    [Header("Inside Event Sound (Step 3)")]
    [SerializeField, Tooltip("门被强制关闭后，在厕所内循环/单次播放的声响（单次）")]
    private AudioClip insideAfterLockClip;

    [SerializeField, Tooltip("Step3 声音参数（音量/范围/衰减）")]
    private Sound3DSettings insideAfterLockSoundSettings = new Sound3DSettings
    {
        volume = 1f,
        minDistance = 2f,
        maxDistance = 25f,
        rolloffMode = AudioRolloffMode.Logarithmic
    };

    [Header("Monster Outside Sound (Step 4)")]
    [SerializeField, Tooltip("门外怪物叫声")]
    private AudioClip monsterOutsideClip;

    [SerializeField, Tooltip("怪物叫声声源点（为空时使用当前对象）")]
    private Transform monsterOutsideAudioPoint;

    [SerializeField, Tooltip("Step4 声音参数（音量/范围/衰减）")]
    private Sound3DSettings monsterOutsideSoundSettings = new Sound3DSettings
    {
        volume = 1f,
        minDistance = 2f,
        maxDistance = 25f,
        rolloffMode = AudioRolloffMode.Logarithmic
    };

    [SerializeField, Min(0f), Tooltip("锁门后到怪物叫声开始的延迟秒数")]
    private float delayBeforeMonsterRoar = 0.6f;

    [Header("Light Flicker (Step 3/5)")]
    [SerializeField, Tooltip("厕所内需要闪烁的灯（可多个）")]
    private Light[] toiletLights;

    [SerializeField, Min(0.01f), Tooltip("闪烁最短间隔")]
    private float flickerMinInterval = 0.05f;

    [SerializeField, Min(0.01f), Tooltip("闪烁最长间隔")]
    private float flickerMaxInterval = 0.18f;

    [Header("Flow")]
    [SerializeField, Tooltip("是否只触发一次整段厕所事件")]
    private bool triggerOnce = true;

    [Header("Debug Runtime")]
    [SerializeField] private bool isActivatedByCondition;
    [SerializeField] private bool nearAreaTriggered;
    [SerializeField] private bool insideTriggered;
    [SerializeField] private bool eventRunning;
    [SerializeField] private bool eventCompleted;

    private Coroutine runningSequence;
    private Coroutine flickerRoutine;

    private void OnValidate()
    {
        if (flickerMaxInterval < flickerMinInterval)
        {
            flickerMaxInterval = flickerMinInterval;
        }

        nearAreaSoundSettings.Clamp();
        insideAfterLockSoundSettings.Clamp();
        monsterOutsideSoundSettings.Clamp();
    }

    private void OnDisable()
    {
        StopFlickerAndRestoreLights();

        if (runningSequence != null)
        {
            StopCoroutine(runningSequence);
            runningSequence = null;
        }
    }

    public void NotifyNearAreaEnter(Collider other)
    {
        if (!IsValidPlayer(other)) return;
        if (triggerOnce && eventCompleted) return;
        if (nearAreaTriggered) return;
        if (!CanTriggerByValueManage()) return;

        nearAreaTriggered = true;
        Play3DClip(nearAreaToiletClip, toiletInnerAudioPoint, nearAreaSoundSettings);
    }

    public void NotifyInsideToiletEnter(Collider other)
    {
        if (!IsValidPlayer(other)) return;
        if (triggerOnce && eventCompleted) return;
        if (insideTriggered || eventRunning) return;
        if (!CanTriggerByValueManage()) return;

        insideTriggered = true;
        eventRunning = true;

        if (runningSequence != null)
        {
            StopCoroutine(runningSequence);
        }
        runningSequence = StartCoroutine(RunToiletSequence());
    }

    private IEnumerator RunToiletSequence()
    {
        if (doorController != null)
        {
            doorController.SetInteractionLocked(true);
            if (closeDoorOnEnter)
            {
                doorController.Close();
            }
        }
        else
        {
            Debug.LogWarning("[ToiletEventController] DoorController is missing.", this);
        }

        Play3DClip(insideAfterLockClip, toiletInnerAudioPoint, insideAfterLockSoundSettings);
        StartFlicker();

        if (delayBeforeMonsterRoar > 0f)
        {
            yield return new WaitForSeconds(delayBeforeMonsterRoar);
        }

        float monsterDuration = Play3DClip(monsterOutsideClip, monsterOutsideAudioPoint, monsterOutsideSoundSettings);
        if (monsterDuration > 0f)
        {
            yield return new WaitForSeconds(monsterDuration);
        }

        StopFlickerAndRestoreLights();

        if (doorController != null)
        {
            doorController.SetInteractionLocked(false);
        }

        eventRunning = false;
        eventCompleted = true;
        runningSequence = null;
    }

    private void StartFlicker()
    {
        if (toiletLights == null || toiletLights.Length == 0)
        {
            return;
        }

        if (flickerRoutine != null)
        {
            StopCoroutine(flickerRoutine);
        }
        flickerRoutine = StartCoroutine(FlickerLoop());
    }

    private IEnumerator FlickerLoop()
    {
        while (true)
        {
            bool nextState = Random.value > 0.35f;
            for (int i = 0; i < toiletLights.Length; i++)
            {
                Light lightComp = toiletLights[i];
                if (lightComp == null) continue;
                lightComp.enabled = nextState;
            }

            float wait = Random.Range(flickerMinInterval, flickerMaxInterval);
            yield return new WaitForSeconds(wait);
        }
    }

    private void StopFlickerAndRestoreLights()
    {
        if (flickerRoutine != null)
        {
            StopCoroutine(flickerRoutine);
            flickerRoutine = null;
        }

        if (toiletLights == null) return;
        for (int i = 0; i < toiletLights.Length; i++)
        {
            Light lightComp = toiletLights[i];
            if (lightComp == null) continue;
            lightComp.enabled = true;
        }
    }

    private bool IsValidPlayer(Collider other)
    {
        return other != null && other.CompareTag("Player");
    }

    private bool CanTriggerByValueManage()
    {
        isActivatedByCondition = IsActivatedByValueManage();
        return isActivatedByCondition;
    }

    private bool IsActivatedByValueManage()
    {
        if (valueManage == null) valueManage = FindFirstObjectByType<ValueManage>();
        if (valueManage == null) return false;

        bool dayOk = targetDay < 0 || valueManage.day == targetDay;
        bool situationOk = string.IsNullOrWhiteSpace(targetSituationId) || valueManage.GetSituation(targetSituationId);
        return dayOk && situationOk;
    }

    /// <summary>
    /// 返回 clip 时长，供调用方等待播放完成。
    /// </summary>
    private float Play3DClip(AudioClip clip, Transform sourcePoint, Sound3DSettings settings)
    {
        if (clip == null)
        {
            return 0f;
        }

        AudioManager manager = AudioManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[ToiletEventController] AudioManager.Instance not found.", this);
            return 0f;
        }

        Transform anchor = sourcePoint != null ? sourcePoint : transform;
        manager.PlaySFX3D(clip, anchor, settings.volume, settings.minDistance, settings.maxDistance, settings.rolloffMode);
        return clip.length;
    }
}
