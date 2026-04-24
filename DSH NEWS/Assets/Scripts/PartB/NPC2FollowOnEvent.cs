using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// npc2 专用：由 <see cref="ValueManage"/> 的 day / situation 决定是否进入跟随模式。
/// 未激活时沿用同物体上的 <see cref="NPCController"/>；激活后跟随玩家，距离大于保持半径则移动，小于等于则停下。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NPC2FollowOnEvent : MonoBehaviour
{
    [Header("Activation (ValueManage)")]
    [SerializeField, Tooltip("与 StoreRoomDoor 一致：-1 表示不检查 day。")]
    private int targetDay = 1;

    [SerializeField, Tooltip("与 StoreRoomDoor 一致：留空表示不检查 situation；否则需 GetSituation 为 true（大小写敏感）。")]
    private string targetSituationId = "A";

    [SerializeField, Tooltip("留空则 FindFirstObjectByType<ValueManage>()。")]
    private ValueManage valueManage;

    [Header("References")]
    [SerializeField, Tooltip("玩家；留空则 Tag=Player。")]
    private Transform player;

    [SerializeField, Tooltip("留空则 GetComponent。")]
    private NavMeshAgent agent;

    [SerializeField, Tooltip("巡逻等行为；留空则 GetComponent。跟随激活时会暂时禁用该组件。")]
    private NPCController npcController;

    [Header("Follow")]
    [SerializeField, Min(0.1f), Tooltip("保持距离（米）：大于此值时朝玩家寻路，小于等于则停止移动。")]
    private float followDistance = 3f;

    [SerializeField, Min(0f), Tooltip("刷新玩家目标点的间隔（秒）。")]
    private float repathInterval = 0.15f;

    [SerializeField, Tooltip("距离是否只算水平 XZ。")]
    private bool useHorizontalDistance = true;

    [SerializeField, Tooltip("在保持距离内停下时是否转向玩家。")]
    private bool facePlayerWhenStopped = true;

    [SerializeField, Min(0f), Tooltip("停下时转向角速度（度/秒）。")]
    private float rotateSpeed = 360f;

    [Header("Debug")]
    [SerializeField, Tooltip("当前 ValueManage 条件是否满足（可跟随）。")]
    private bool isActivatedByCondition;

    private bool followModeActive;
    private float repathTimer;
    private float nextPlayerResolveTime;
    private bool warnedMissingValueManage;

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        npcController = GetComponent<NPCController>();
    }

    private void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (npcController == null) npcController = GetComponent<NPCController>();
        if (valueManage == null) valueManage = FindFirstObjectByType<ValueManage>();
        TryAssignPlayer();
    }

    /// <summary>
    /// 放在 LateUpdate，避免与 <see cref="NPCController"/> 的 Update 抢同一帧的 Agent 控制权。
    /// </summary>
    private void LateUpdate()
    {
        bool wantFollow = IsFollowAllowedByValueManage();
        isActivatedByCondition = wantFollow;

        if (wantFollow != followModeActive)
        {
            followModeActive = wantFollow;
            if (followModeActive)
                EnterFollowMode();
            else
                ExitFollowMode();
        }

        if (!followModeActive) return;

        if (player == null)
        {
            if (Time.time >= nextPlayerResolveTime)
            {
                nextPlayerResolveTime = Time.time + 0.5f;
                TryAssignPlayer();
            }

            if (player == null) return;
        }

        if (agent == null) return;

        repathTimer -= Time.deltaTime;
        float keepSqr = followDistance * followDistance;
        float sqr = SqrDistanceToPlayer();

        if (sqr > keepSqr)
        {
            if (agent.isStopped) agent.isStopped = false;

            if (repathTimer <= 0f)
            {
                repathTimer = repathInterval;
                agent.SetDestination(player.position);
            }
        }
        else
        {
            if (!agent.isStopped) agent.isStopped = true;

            if (facePlayerWhenStopped) RotateTowardsPlayer();
        }
    }

    private bool IsFollowAllowedByValueManage()
    {
        if (valueManage == null) valueManage = FindFirstObjectByType<ValueManage>();

        if (valueManage == null)
        {
            if (!warnedMissingValueManage)
            {
                warnedMissingValueManage = true;
                Debug.LogWarning("[NPC2FollowOnEvent] 找不到 ValueManage，不会进入跟随模式。", this);
            }

            return false;
        }

        bool dayOk = targetDay == -1 || valueManage.day == targetDay;
        bool situationOk = string.IsNullOrWhiteSpace(targetSituationId) || valueManage.GetSituation(targetSituationId);
        return dayOk && situationOk;
    }

    private void EnterFollowMode()
    {
        if (npcController == null)
        {
            Debug.LogWarning("[NPC2FollowOnEvent] 未找到 NPCController，无法在进入跟随时关闭巡逻逻辑，可能与 NavMeshAgent 冲突。", this);
        }
        else
        {
            npcController.enabled = false;
        }

        if (agent != null)
        {
            agent.isStopped = false;
            repathTimer = 0f;
        }
    }

    private void ExitFollowMode()
    {
        if (agent != null)
        {
            agent.ResetPath();
            agent.isStopped = false;
        }

        if (npcController != null) npcController.enabled = true;
    }

    private void OnDisable()
    {
        if (!followModeActive) return;

        followModeActive = false;
        ExitFollowMode();
    }

    private void TryAssignPlayer()
    {
        if (player != null) return;

        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) player = go.transform;
    }

    private float SqrDistanceToPlayer()
    {
        if (useHorizontalDistance)
        {
            Vector3 a = transform.position;
            Vector3 b = player.position;
            a.y = 0f;
            b.y = 0f;
            return (b - a).sqrMagnitude;
        }

        return (player.position - transform.position).sqrMagnitude;
    }

    private void RotateTowardsPlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }
}
