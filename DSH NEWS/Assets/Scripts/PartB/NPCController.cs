using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCController : MonoBehaviour
{
    /// <summary>
    /// NPC 行为状态：仅保留需要的状态以简化逻辑。
    /// - Idle: 空闲，不移动。
    /// - Patrol: 按 patrolPoints 巡逻。
    /// - VisitingInterest: 临时偏离巡逻去访问兴趣点（兴趣点可能触发停留动作）。
    /// - Stop: 停止（用于被玩家阻挡或显式暂停）。
    /// </summary>
    private enum State
    {
        Idle,
        Patrol,
        VisitingInterest,
        Stop
    }

    [Header("References")]
    [SerializeField, Tooltip("可选：Animator（用于接收速度以驱动动画），若留空会尝试自动获取）。")]
    private Animator animator;

    [SerializeField, Tooltip("NavMeshAgent（自动获取）。")]
    private NavMeshAgent agent;

    [Header("Animator")]
    [SerializeField, Tooltip("Animator 中接收速度的 Float 参数名（例如 Speed）。")]
    private string speedParam = "Speed";

    [Header("Patrol")]
    [SerializeField, Tooltip("按顺序的巡逻点（为空则不巡逻）。")]
    private Transform[] patrolPoints;

    [SerializeField, Tooltip("到达巡逻点后的等待时间（秒）。<=0 表示不巡逻时停止在最后一个点）。")]
    private float patrolWaitTime = 0f;

    [SerializeField, Tooltip("巡逻是否循环（到末尾回到开头）。")]
    private bool patrolLoop = true;

    [SerializeField, Tooltip("判定已到达目标点的最小距离（米）。")]
    private float arriveThreshold = 0.2f;

    [SerializeField, Tooltip("在路点周围随机偏移的半径（米），避免每次踩在精确点上）。")]
    private float patrolPointRadius = 0.5f;

    [Header("Interest Points")]
    [SerializeField, Tooltip("兴趣点列表：NPC 有概率偏离路线前往这些点执行短时动作。")]
    private Transform[] interestPoints;

    [SerializeField, Range(0f, 1f), Tooltip("到达巡逻点后偏离去兴趣点的概率。")]
    private float interestChance = 0.2f;

    [SerializeField, Tooltip("在兴趣点停留/执行动作的时长（秒）。")]
    private float interestActionTime = 3f;

    [SerializeField, Tooltip("兴趣点目标随机偏移半径，避免总踩中心）。")]
    private float interestPointRadius = 0.5f;

    [Header("Player Blocking")]
    [SerializeField, Tooltip("判定玩家的 Layer（把 Player 放到该 Layer）。")]
    private LayerMask playerLayer;

    [SerializeField, Tooltip("检测玩家是否阻挡的半径（米）。")]
    private float playerBlockRadius = 1.0f;

    [SerializeField, Range(0f, 180f), Tooltip("只有当玩家位于 NPC 前方该角度范围内才视为阻挡（度）。")]
    private float playerBlockAngle = 90f;

    [SerializeField, Tooltip("是否要求与玩家有直线视野（射线不被障碍遮挡）才判定阻挡）。")]
    private bool requireLineOfSight = true;

    // 运行时字段
    private State currentState = State.Idle;
    private int patrolIndex = 0;
    private Coroutine patrolWaitCoroutine;
    private Coroutine interestCoroutine;

    // 暂停/恢复相关
    private bool pausedByPlayer = false;                 // 是否因为玩家阻挡或交互而暂停
    private State prevStateBeforePause = State.Idle;    // 暂停前的状态，用于恢复
    private Vector3 lastDestination;                    // 暂停前的目标，用于恢复目的地

    private void Reset()
    {
        // 在 Inspector 的 Reset 操作或首次添加组件时自动抓取必需组件，方便配置
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        // 确保必要引用存在
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponent<Animator>();

        // 由 NavMeshAgent 管理旋转以更好配合其寻路（若你希望手动旋转，可改为 false 并自行处理）
        if (agent != null)
            agent.updateRotation = true;

        // 由 Agent 驱动位移时通常关闭 Animator 的 Root Motion
        if (animator != null)
            animator.applyRootMotion = false;
    }

    private void Start()
    {
        // 启动时如果配置了巡逻点，进入巡逻状态并设置第一个目标（带随机偏移）
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            patrolIndex = 0;
            currentState = State.Patrol;
            agent.isStopped = false;
            var dest = GetRandomizedDestination(patrolPoints[patrolIndex].position, patrolPointRadius);
            lastDestination = dest;
            agent.SetDestination(dest);
        }
        else
        {
            currentState = State.Idle;
        }
    }

    private void Update()
    {
        if (agent == null) return;

        // 1) 检测玩家阻挡（若不是已被显式暂停）
        // 若玩家阻挡则 PauseForPlayer()，玩家离开后 ResumeFromPlayer()
        if (!pausedByPlayer && IsPlayerBlocking())
        {
            PauseForPlayer();
        }
        else if (pausedByPlayer && !IsPlayerBlocking())
        {
            ResumeFromPlayer();
        }

        // 2) 将 NavMeshAgent 的实际速度（m/s）传给 Animator 的 speed 参数，用于 BlendTree/过渡
        if (animator != null && !string.IsNullOrEmpty(speedParam))
        {
            animator.SetFloat(speedParam, agent.velocity.magnitude);
        }

        // 3) 若处于被玩家暂停状态则不继续行为逻辑（agent.isStopped 已在 PauseForPlayer 中设置）
        if (pausedByPlayer) return;

        // 4) 根据当前状态执行逻辑
        switch (currentState)
        {
            case State.Idle:
                // 可在此处添加空闲行为
                break;
            case State.Patrol:
                UpdatePatrol();
                break;
            case State.VisitingInterest:
                UpdateVisitingInterest();
                break;
            case State.Stop:
                if (!agent.isStopped) agent.isStopped = true;
                break;
        }
    }

    // ========== 玩家阻挡判定与暂停/恢复逻辑 ==========

    /// <summary>
    /// 判断玩家是否在 NPC 前方一定半径与角度范围内并（可选）有视线。
    /// 若满足则认为玩家在“挡路”并返回 true。
    /// </summary>
    private bool IsPlayerBlocking()
    {
        if (playerLayer == 0) return false; // 未配置 layer mask 则不判定
        Collider[] hits = Physics.OverlapSphere(transform.position, playerBlockRadius, playerLayer);
        if (hits == null || hits.Length == 0) return false;

        foreach (var col in hits)
        {
            if (col == null) continue;
            // 只考虑水平方向（忽略高度差）
            Vector3 dir = (col.transform.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) continue;
            float angle = Vector3.Angle(transform.forward, dir);
            if (angle > playerBlockAngle * 0.5f) continue;

            if (requireLineOfSight)
            {
                // 从 NPC 适当高度向玩家射线，检测首个命中是否为玩家自身
                Vector3 origin = transform.position + Vector3.up * 0.5f;
                Vector3 to = (col.transform.position + Vector3.up * 0.9f) - origin;
                if (Physics.Raycast(origin, to.normalized, out RaycastHit hit, to.magnitude))
                {
                    if (hit.collider != col) continue; // 有障碍遮挡，则视为不可见
                }
                else
                {
                    continue;
                }
            }

            // 满足所有判定条件 -> 认为玩家挡路
            return true;
        }
        return false;
    }

    /// <summary>
    /// 被玩家阻挡或交互时暂停 NPC：
    /// - 记录当前状态与目标以便恢复
    /// - 设置 agent.isStopped = true
    /// - 把当前状态置为 Stop
    /// </summary>
    private void PauseForPlayer()
    {
        if (pausedByPlayer) return;
        pausedByPlayer = true;
        prevStateBeforePause = currentState;
        lastDestination = agent.hasPath ? agent.destination : lastDestination;
        agent.isStopped = true;
        currentState = State.Stop;
    }

    /// <summary>
    /// 玩家离开或交互结束后恢复 NPC：
    /// - 恢复暂停前状态，若存在保存的目的地则重新设置
    /// - 解除暂停标记并启用 agent
    /// </summary>
    private void ResumeFromPlayer()
    {
        if (!pausedByPlayer) return;
        pausedByPlayer = false;
        currentState = prevStateBeforePause;
        agent.isStopped = false;
        if (lastDestination != Vector3.zero)
            agent.SetDestination(lastDestination);
    }

    /// <summary>
    /// 外部显式调用：玩家开始交互时（例如 PlayerController 调用），暂停 NPC。
    /// </summary>
    public void OnPlayerInteractStart()
    {
        PauseForPlayer();
    }

    /// <summary>
    /// 外部显式调用：玩家结束交互时（例如 PlayerController 调用），恢复 NPC。
    /// </summary>
    public void OnPlayerInteractEnd()
    {
        ResumeFromPlayer();
    }

    // ========== 巡逻与兴趣点逻辑 ==========

    /// <summary>
    /// 巡逻逻辑：当到达巡逻点时，按概率偏离去兴趣点；否则等待或继续下个巡逻点。
    /// </summary>
    private void UpdatePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) { currentState = State.Idle; return; }
        if (agent.pathPending) return;

        if (!agent.hasPath || agent.remainingDistance <= arriveThreshold)
        {
            // 到达巡逻点：先尝试访问兴趣点
            if (ShouldVisitInterest())
            {
                Transform ip = ChooseInterestPoint();
                if (ip != null)
                {
                    StartVisitingInterest(ip);
                    return;
                }
            }

            // 否则等待或前往下一巡逻点
            if (patrolWaitTime > 0f && patrolWaitCoroutine == null)
            {
                patrolWaitCoroutine = StartCoroutine(PatrolWait());
            }
            else if (patrolWaitTime <= 0f)
            {
                AdvancePatrol();
            }
        }
    }

    /// <summary>
    /// 访问兴趣点时的更新：到达兴趣点后启动停留动作协程。
    /// </summary>
    private void UpdateVisitingInterest()
    {
        if (agent.pathPending) return;

        if (!agent.hasPath || agent.remainingDistance <= arriveThreshold)
        {
            if (interestCoroutine == null)
            {
                interestCoroutine = StartCoroutine(InterestActionCoroutine());
            }
        }
    }

    /// <summary>
    /// 是否按概率选择去访问兴趣点（根据 interestChance）。
    /// </summary>
    private bool ShouldVisitInterest()
    {
        if (interestPoints == null || interestPoints.Length == 0) return false;
        return Random.value <= interestChance;
    }

    /// <summary>
    /// 随机选择一个兴趣点（简单随机，未去重或权重）。
    /// </summary>
    private Transform ChooseInterestPoint()
    {
        if (interestPoints == null || interestPoints.Length == 0) return null;
        int idx = Random.Range(0, interestPoints.Length);
        return interestPoints[idx];
    }

    /// <summary>
    /// 开始前往并访问指定兴趣点：设置状态、目标与记录目标用于恢复。
    /// </summary>
    private void StartVisitingInterest(Transform interest)
    {
        if (interest == null) { AdvancePatrol(); return; }
        currentState = State.VisitingInterest;
        Vector3 dest = GetRandomizedDestination(interest.position, interestPointRadius);
        agent.isStopped = false;
        lastDestination = dest;
        agent.SetDestination(dest);
    }

    /// <summary>
    /// 在兴趣点执行动作的协程（停留一段时间），完成后返回巡逻流。
    /// </summary>
    private IEnumerator InterestActionCoroutine()
    {
        agent.isStopped = true;
        yield return new WaitForSeconds(Mathf.Max(0f, interestActionTime));
        agent.isStopped = false;
        interestCoroutine = null;

        AdvancePatrol();
        currentState = (patrolPoints != null && patrolPoints.Length > 0) ? State.Patrol : State.Idle;
    }

    /// <summary>
    /// 巡逻点等待协程，等待结束后继续前往下一个巡逻点。
    /// </summary>
    private IEnumerator PatrolWait()
    {
        agent.isStopped = true;
        yield return new WaitForSeconds(patrolWaitTime);
        agent.isStopped = false;
        patrolWaitCoroutine = null;
        AdvancePatrol();
    }

    /// <summary>
    /// 推进到下一个巡逻点并下达带随机偏移的目的地。
    /// </summary>
    private void AdvancePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) { currentState = State.Idle; return; }

        if (patrolLoop)
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        else if (patrolIndex < patrolPoints.Length - 1)
            patrolIndex++;

        Vector3 target = GetRandomizedDestination(patrolPoints[patrolIndex].position, patrolPointRadius);
        lastDestination = target;
        agent.SetDestination(target);
    }

    /// <summary>
    /// 在 basePosition 周围按给定 radius 随机采样一个点，并使用 NavMesh.SamplePosition 返回最近可达点（失败则返回 basePosition）。
    /// 这样可以保证目标点位于 NavMesh 上，避免设置不可达目标。
    /// </summary>
    private Vector3 GetRandomizedDestination(Vector3 basePosition, float radius)
    {
        if (radius <= 0f) return basePosition;

        Vector2 rnd = Random.insideUnitCircle * radius;
        Vector3 candidate = basePosition + new Vector3(rnd.x, 0f, rnd.y);

        NavMeshHit hit;
        float sampleDistance = Mathf.Max(0.1f, radius);
        if (NavMesh.SamplePosition(candidate, out hit, sampleDistance, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return basePosition;
    }
}