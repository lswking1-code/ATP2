using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StoreRoomDoor : MonoBehaviour
{
    [Header("Activation Condition")]
    [SerializeField, Tooltip("仅当 day 精确等于该值时允许触发剧情")]
    private int targetDay = 1;

    [SerializeField, Tooltip("仅当该 Situation 已发生时允许触发（大小写敏感）")]
    private string targetSituationId = "A";

    [SerializeField, Tooltip("ValueManage 引用；为空时会自动查找场景中的 ValueManage")]
    private ValueManage valueManage;

    [Header("Door")]
    [SerializeField, Tooltip("剧情门控制器")]
    private DoorController doorController;

    [SerializeField, Tooltip("外层触发时相对关门角度的微开增量（默认 Y=20）")]
    private Vector3 slightOpenDeltaEuler = new Vector3(0f, 20f, 0f);

    [SerializeField, Tooltip("外层触发后门打开时长（秒）")]
    private float slightOpenDuration = 0.35f;

    [Header("Audio")]
    [SerializeField, Tooltip("外层触发后在门附近播放的提示音")]
    private AudioClip doorCueClip;

    [SerializeField, Tooltip("近距触发后在房间内播放的诡异声")]
    private AudioClip roomCreepyClip;

    [SerializeField, Tooltip("玩家靠近触发关门瞬间播放的关门声（3D，与 DoorController 默认关声可分开配）")]
    private AudioClip nearTriggerCloseDoorClip;

    [SerializeField, Tooltip("门附近的 3D 声源点；为空时用当前物体")]
    private Transform doorCueSourcePoint;

    [SerializeField, Tooltip("房内诡异声的 3D 声源点；为空时用当前物体")]
    private Transform roomCreepySourcePoint;

    [SerializeField, Tooltip("近距关门声源点；空则优先用门提示音点，否则用当前物体")]
    private Transform nearCloseDoorSourcePoint;

    [SerializeField, Range(0f, 1f)]
    private float volume = 0.9f;

    [SerializeField, Min(0f)]
    private float minDistance = 2f;

    [SerializeField, Min(0f)]
    private float maxDistance = 25f;

    [SerializeField]
    private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    [Header("Debug Runtime State")]
    [SerializeField, Tooltip("运行时：条件是否满足")]
    private bool isActivatedByCondition;

    [SerializeField, Tooltip("运行时：是否已触发外层开缝事件")]
    private bool hasOuterTriggered;

    [SerializeField, Tooltip("运行时：是否已触发近距关门事件")]
    private bool hasCloseTriggered;

    [SerializeField, Tooltip("运行时：整段是否已完成（完成后不再触发）")]
    private bool isSequenceCompleted;

    private Coroutine autoCloseRoutine;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Awake()
    {
        if (valueManage == null)
        {
            valueManage = FindFirstObjectByType<ValueManage>();
        }
    }

    private void Start()
    {
        EvaluateActivationCondition();
    }

    private void OnValidate()
    {
        if (maxDistance < minDistance)
        {
            maxDistance = minDistance;
        }
    }

    private void OnDisable()
    {
        if (autoCloseRoutine != null)
        {
            StopCoroutine(autoCloseRoutine);
            autoCloseRoutine = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (!CanRunOuterStage())
        {
            return;
        }

        hasOuterTriggered = true;
        TriggerSlightOpenStage();
    }

    public void NotifyNearDoorPlayerEnter(Collider other)
    {
        if (other == null || !other.CompareTag("Player"))
        {
            return;
        }

        if (!CanRunNearStage())
        {
            return;
        }

        hasCloseTriggered = true;
        TriggerCloseStage();
        isSequenceCompleted = true;
    }

    private void EvaluateActivationCondition()
    {
        if (valueManage == null)
        {
            isActivatedByCondition = false;
            Debug.LogWarning("[StoreRoomDoor] ValueManage is missing, sequence disabled.", this);
            return;
        }

        bool dayMatched = targetDay == -1 || valueManage.day == targetDay;
        bool situationMatched = string.IsNullOrWhiteSpace(targetSituationId) || valueManage.GetSituation(targetSituationId);

        isActivatedByCondition = dayMatched && situationMatched;
    }

    private bool CanRunOuterStage()
    {
        if (isSequenceCompleted || hasOuterTriggered)
        {
            return false;
        }

        EvaluateActivationCondition();
        return isActivatedByCondition;
    }

    private bool CanRunNearStage()
    {
        if (isSequenceCompleted || hasCloseTriggered)
        {
            return false;
        }

        if (!hasOuterTriggered)
        {
            return false;
        }

        EvaluateActivationCondition();
        return isActivatedByCondition;
    }

    private void TriggerSlightOpenStage()
    {
        if (doorController == null)
        {
            Debug.LogWarning("[StoreRoomDoor] DoorController is missing, cannot open/close door.", this);
        }
        else
        {
            doorController.OpenByDeltaFromClosed(slightOpenDeltaEuler);

            if (autoCloseRoutine != null)
            {
                StopCoroutine(autoCloseRoutine);
            }
            autoCloseRoutine = StartCoroutine(AutoCloseAfterDelay(slightOpenDuration));
        }

        Play3DClip(doorCueClip, doorCueSourcePoint);
    }

    private void TriggerCloseStage()
    {
        if (autoCloseRoutine != null)
        {
            StopCoroutine(autoCloseRoutine);
            autoCloseRoutine = null;
        }

        if (doorController == null)
        {
            Debug.LogWarning("[StoreRoomDoor] DoorController is missing, cannot close door.", this);
        }
        else
        {
            bool hasNearCloseSfx = nearTriggerCloseDoorClip != null;
            doorController.Close(playSound: !hasNearCloseSfx);
        }

        Transform nearCloseAnchor = nearCloseDoorSourcePoint != null ? nearCloseDoorSourcePoint : doorCueSourcePoint;
        Play3DClip(nearTriggerCloseDoorClip, nearCloseAnchor);
        Play3DClip(roomCreepyClip, roomCreepySourcePoint);
    }

    private System.Collections.IEnumerator AutoCloseAfterDelay(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (!hasCloseTriggered && !isSequenceCompleted && doorController != null)
        {
            doorController.Close();
        }

        autoCloseRoutine = null;
    }

    private void Play3DClip(AudioClip clip, Transform sourcePoint)
    {
        if (clip == null)
        {
            return;
        }

        AudioManager manager = AudioManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[StoreRoomDoor] AudioManager.Instance not found.", this);
            return;
        }

        Transform anchor = sourcePoint != null ? sourcePoint : transform;
        manager.PlaySFX3D(clip, anchor, volume, minDistance, maxDistance, rolloffMode);
    }
}
