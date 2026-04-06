using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCController : MonoBehaviour
{
    /// <summary>
    /// NPC 行为状态（简化）
    /// </summary>
    private enum State
    {
        Idle,
        Patrol,
        VisitingInterest,
        Stop
    }

    [Header("References")]
    [SerializeField, Tooltip("可选：Animator（用于接收速度与坡度以驱动动画），若留空会尝试自动获取）。")]
    private Animator animator;

    [SerializeField, Tooltip("NavMeshAgent（自动获取）。")]
    private NavMeshAgent agent;

    [Header("Animator")]
    [SerializeField, Tooltip("Animator 中接收速度的 Float 参数名（例如 Speed）。")]
    private string speedParam = "Speed";

    [SerializeField, Tooltip("Animator 中接收坡度的 Float 参数名，范围约定为 -1 (下坡) 到 +1 (上坡)。")]
    private string slopeParam = "Slope";

    [SerializeField, Tooltip("将坡度值传入 Animator 时的阻尼时间（秒），用于平滑过渡）。")]
    private float slopeDampTime = 0.12f;

    [SerializeField, Tooltip("水平速度平滑时间（秒），用于在开始/停止时平滑过渡动画参数）。")]
    private float speedSmoothTime = 0.15f;

    // 平滑状态缓存（不在 Inspector 显示）
    private float smoothedSpeed = 0f;
    private float smoothedSpeedVel = 0f;

    [Header("Patrol")]
    [SerializeField, Tooltip("按顺序的巡逻点（为空则不巡逻）。")]
    private Transform[] patrolPoints;

    [SerializeField, Tooltip("到达巡逻点后的等待时间（秒）。<=0 表示不等待）。")]
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

    [Header("Slope Detection")]
    [SerializeField, Tooltip("将垂直高度差归一化为坡度值的阈值（米）；dy / threshold 映射到 [-1,1]。")]
    private float slopeNormalizeThreshold = 0.25f;

    [SerializeField, Tooltip("检测坡度时的最小移动速度（m/s），低于则不更新坡度）。")]
    private float minSpeedToDetectSlope = 0.05f;

    // 运行时字段
    private State currentState = State.Idle;
    private int patrolIndex = 0;
    private Coroutine patrolWaitCoroutine;
    private Coroutine interestCoroutine;

    // 暂停/恢复相关
    private bool pausedByPlayer = false;
    private State prevStateBeforePause = State.Idle;
    private Vector3 lastDestination;

    // 楼面高度缓存（用于坡度计算）
    private float lastGroundY;

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponent<Animator>();

        if (agent != null)
            agent.updateRotation = true;

        if (animator != null)
            animator.applyRootMotion = false;

        // 初始化地面高度缓存
        lastGroundY = SampleGroundY(transform.position);
    }

    private void Start()
    {
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

        // 检测玩家阻挡并暂停/恢复
        if (!pausedByPlayer && IsPlayerBlocking())
        {
            PauseForPlayer();
        }
        else if (pausedByPlayer && !IsPlayerBlocking())
        {
            ResumeFromPlayer();
        }

        // 更新坡度参数（始终写入 Animator 的 Slope）
        UpdateSlopeParam();

        // 平滑水平速度并写入 Animator（避免动画突变）
        UpdateSmoothedSpeedAndApply();

        if (pausedByPlayer) return;

        switch (currentState)
        {
            case State.Idle:
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

    // 采样 NavMesh 上的地面 Y，失败退化为 worldPos.y
    private float SampleGroundY(Vector3 worldPos)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(worldPos, out hit, 1.0f, NavMesh.AllAreas))
            return hit.position.y;
        return worldPos.y;
    }

    // 计算并写入 Animator.Slope（-1..1）
    private void UpdateSlopeParam()
    {
        if (animator == null || string.IsNullOrEmpty(slopeParam)) return;

        float speed = agent.velocity.magnitude;
        float currentGroundY = SampleGroundY(agent.nextPosition);
        float dy = currentGroundY - lastGroundY;

        float slopeValue = 0f;
        if (speed >= minSpeedToDetectSlope && Mathf.Abs(dy) > 0.0001f)
        {
            slopeValue = Mathf.Clamp(dy / Mathf.Max(0.0001f, slopeNormalizeThreshold), -1f, 1f);
        }

        // 将坡度值写入 Animator，并使用阻尼平滑
        animator.SetFloat(slopeParam, slopeValue, slopeDampTime, Time.deltaTime);

        // 平滑更新缓存，避免抖动
        lastGroundY = Mathf.Lerp(lastGroundY, currentGroundY, 0.5f);
    }

    // 平滑水平速度并应用到 Animator 的 speed 参数
    private void UpdateSmoothedSpeedAndApply()
    {
        if (animator == null || string.IsNullOrEmpty(speedParam)) return;

        // 取水平分量速度（忽略 y）
        Vector3 horizVel = agent.velocity;
        horizVel.y = 0f;
        float targetSpeed = horizVel.magnitude;

        // 如果 agent 被停止（例如被玩家挡住），目标速度应为 0
        if (agent.isStopped) targetSpeed = 0f;

        // 平滑过渡当前速度到目标速度
        smoothedSpeed = Mathf.SmoothDamp(smoothedSpeed, targetSpeed, ref smoothedSpeedVel, speedSmoothTime);

        // 将平滑后的速度写入 Animator（我们已在这里做平滑，因此使用无阻尼的 SetFloat 重载）
        animator.SetFloat(speedParam, smoothedSpeed);
    }

    // ========== 玩家阻挡判定、暂停/恢复（同前） ==========
    private bool IsPlayerBlocking()
    {
        if (playerLayer == 0) return false;
        Collider[] hits = Physics.OverlapSphere(transform.position, playerBlockRadius, playerLayer);
        if (hits == null || hits.Length == 0) return false;

        foreach (var col in hits)
        {
            if (col == null) continue;
            Vector3 dir = col.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) continue;
            float angle = Vector3.Angle(transform.forward, dir);
            if (angle > playerBlockAngle * 0.5f) continue;

            if (requireLineOfSight)
            {
                Vector3 origin = transform.position + Vector3.up * 0.5f;
                Vector3 to = (col.transform.position + Vector3.up * 0.9f) - origin;
                if (Physics.Raycast(origin, to.normalized, out RaycastHit hit, to.magnitude))
                {
                    if (hit.collider != col) continue;
                }
                else
                {
                    continue;
                }
            }

            return true;
        }
        return false;
    }

    private void PauseForPlayer()
    {
        if (pausedByPlayer) return;
        pausedByPlayer = true;
        prevStateBeforePause = currentState;
        lastDestination = agent.hasPath ? agent.destination : lastDestination;
        agent.isStopped = true;
        currentState = State.Stop;
    }

    private void ResumeFromPlayer()
    {
        if (!pausedByPlayer) return;
        pausedByPlayer = false;
        currentState = prevStateBeforePause;
        agent.isStopped = false;
        if (lastDestination != Vector3.zero)
            agent.SetDestination(lastDestination);
    }

    public void OnPlayerInteractStart() => PauseForPlayer();
    public void OnPlayerInteractEnd() => ResumeFromPlayer();

    // ========== 巡逻与兴趣点逻辑（不变） ==========
    private void UpdatePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) { currentState = State.Idle; return; }
        if (agent.pathPending) return;

        if (!agent.hasPath || agent.remainingDistance <= arriveThreshold)
        {
            if (ShouldVisitInterest())
            {
                Transform ip = ChooseInterestPoint();
                if (ip != null)
                {
                    StartVisitingInterest(ip);
                    return;
                }
            }

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

    private bool ShouldVisitInterest()
    {
        if (interestPoints == null || interestPoints.Length == 0) return false;
        return Random.value <= interestChance;
    }

    private Transform ChooseInterestPoint()
    {
        if (interestPoints == null || interestPoints.Length == 0) return null;
        int idx = Random.Range(0, interestPoints.Length);
        return interestPoints[idx];
    }

    private void StartVisitingInterest(Transform interest)
    {
        if (interest == null) { AdvancePatrol(); return; }
        currentState = State.VisitingInterest;
        Vector3 dest = GetRandomizedDestination(interest.position, interestPointRadius);
        agent.isStopped = false;
        lastDestination = dest;
        agent.SetDestination(dest);
    }

    private IEnumerator InterestActionCoroutine()
    {
        agent.isStopped = true;
        yield return new WaitForSeconds(Mathf.Max(0f, interestActionTime));
        agent.isStopped = false;
        interestCoroutine = null;

        AdvancePatrol();
        currentState = (patrolPoints != null && patrolPoints.Length > 0) ? State.Patrol : State.Idle;
    }

    private IEnumerator PatrolWait()
    {
        agent.isStopped = true;
        yield return new WaitForSeconds(patrolWaitTime);
        agent.isStopped = false;
        patrolWaitCoroutine = null;
        AdvancePatrol();
    }

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