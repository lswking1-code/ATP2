using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCController : MonoBehaviour
{
    /// <summary>
    /// NPC 行为状态枚举。
    /// </summary>
    private enum State
    {
        Idle,
        Patrol,
        VisitingInterest,
        Stop
    }

    [Header("References")]
    [SerializeField, Tooltip("可选 Animator。用于更新速度和坡度参数；未指定时自动尝试获取。")]
    private Animator animator;

    [SerializeField, Tooltip("NavMeshAgent 组件；未指定时自动获取。")]
    private NavMeshAgent agent;

    [Header("Animator")]
    [SerializeField, Tooltip("Animator 中表示速度的 Float 参数名，例如 Speed。")]
    private string speedParam = "Speed";

    [SerializeField, Tooltip("Animator 中表示坡度的 Float 参数名，范围通常为 -1（下坡）到 +1（上坡）。")]
    private string slopeParam = "Slope";

    [SerializeField, Tooltip("写入坡度参数时的阻尼时间（秒），用于平滑过渡。")]
    private float slopeDampTime = 0.12f;

    [SerializeField, Tooltip("水平速度平滑时间（秒），用于起步/停下时减小突变。")]
    private float speedSmoothTime = 0.15f;

    // 平滑速度缓存（不在 Inspector 显示）
    private float smoothedSpeed = 0f;
    private float smoothedSpeedVel = 0f;

    [Header("Patrol")]
    [SerializeField, Tooltip("按顺序巡逻点列表（为空则不巡逻）。")]
    private Transform[] patrolPoints;

    [SerializeField, Tooltip("到达巡逻点后的等待时间（秒），<=0 表示不等待。")]
    private float patrolWaitTime = 0f;

    [SerializeField, Tooltip("是否循环巡逻（末尾回到起点）。")]
    private bool patrolLoop = true;

    [SerializeField, Tooltip("判定到达目标点的最小距离（米）。")]
    private float arriveThreshold = 0.2f;

    [SerializeField, Tooltip("巡逻点随机偏移半径（米），避免每次走到同一点。")]
    private float patrolPointRadius = 0.5f;

    [Header("Interest Points")]
    [SerializeField, Tooltip("兴趣点列表。NPC 可能会偏离巡逻去这些点停留。")]
    private Transform[] interestPoints;

    [SerializeField, Range(0f, 1f), Tooltip("到达巡逻点后转去兴趣点的概率。")]
    private float interestChance = 0.2f;

    [SerializeField, Tooltip("在兴趣点停留/执行动作的时长（秒）。")]
    private float interestActionTime = 3f;

    [SerializeField, Tooltip("兴趣点目标随机偏移半径。")]
    private float interestPointRadius = 0.5f;

    [Header("Player Blocking")]
    [SerializeField, Tooltip("玩家所在 Layer（请把 Player 放到对应 Layer）。")]
    private LayerMask playerLayer;

    [SerializeField, Tooltip("检测玩家阻挡的半径（米）。")]
    private float playerBlockRadius = 1.0f;

    [SerializeField, Range(0f, 180f), Tooltip("仅当前方该角度范围内的玩家才算阻挡。")]
    private float playerBlockAngle = 90f;

    [SerializeField, Tooltip("是否要求视线可达（Raycast 不被遮挡）才判定为阻挡。")]
    private bool requireLineOfSight = true;

    [Header("Forced Look")]
    [SerializeField, Tooltip("强制看向目标时的旋转速度（度/秒）。")]
    private float forcedLookRotateSpeed = 360f;

    [Header("Slope Detection")]
    [SerializeField, Tooltip("高度差归一化阈值（米），dy / threshold 映射到 [-1,1]。")]
    private float slopeNormalizeThreshold = 0.25f;

    [SerializeField, Tooltip("检测坡度时的最小移动速度（m/s），低于该值不更新坡度。")]
    private float minSpeedToDetectSlope = 0.05f;

    // 运行时状态
    private State currentState = State.Idle;
    private int patrolIndex = 0;
    private Coroutine patrolWaitCoroutine;
    private Coroutine interestCoroutine;

    // 暂停/恢复相关
    private bool pausedByPlayer = false;
    private State prevStateBeforePause = State.Idle;
    private Vector3 lastDestination;

    // 地面高度缓存（用于坡度计算）
    private float lastGroundY;
    private bool forceLookAtTarget = false;
    private Transform forcedLookTarget;

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

        // 更新坡度参数（持续写入 Animator 的 Slope）
        UpdateSlopeParam();

        // 平滑水平速度并写入 Animator（避免动画突变）
        UpdateSmoothedSpeedAndApply();

        if (forceLookAtTarget)
            UpdateForcedLookAt();

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

    // 采样 NavMesh 上的地面 Y，失败时返回 worldPos.y
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

        // 写入 Animator，并使用阻尼平滑
        animator.SetFloat(slopeParam, slopeValue, slopeDampTime, Time.deltaTime);

        // 平滑更新缓存，减少抖动
        lastGroundY = Mathf.Lerp(lastGroundY, currentGroundY, 0.5f);
    }

    // 平滑水平速度并应用到 Animator 的 speed 参数
    private void UpdateSmoothedSpeedAndApply()
    {
        if (animator == null || string.IsNullOrEmpty(speedParam)) return;

        // 仅使用水平分量，忽略 y
        Vector3 horizVel = agent.velocity;
        horizVel.y = 0f;
        float targetSpeed = horizVel.magnitude;

        // 停止时速度归零
        if (agent.isStopped) targetSpeed = 0f;

        // 平滑过渡到目标速度
        smoothedSpeed = Mathf.SmoothDamp(smoothedSpeed, targetSpeed, ref smoothedSpeedVel, speedSmoothTime);

        // 写回 Animator
        animator.SetFloat(speedParam, smoothedSpeed);
    }

    // ========== 玩家阻挡判定、暂停/恢复 ==========
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

    // 启用强制朝向某个目标（例如玩家）。
    // 仅控制朝向，不直接改巡逻点数据。
    public void SetForcedLookTarget(Transform target)
    {
        forcedLookTarget = target;
        forceLookAtTarget = target != null;
    }

    // 关闭强制朝向，恢复 NPC 原有朝向逻辑。
    public void ClearForcedLookTarget()
    {
        forceLookAtTarget = false;
        forcedLookTarget = null;
    }

    // 每帧将 NPC 的水平朝向缓慢旋转到目标方向（忽略 y，避免抬头低头）。
    private void UpdateForcedLookAt()
    {
        if (forcedLookTarget == null)
        {
            forceLookAtTarget = false;
            return;
        }

        Vector3 dir = forcedLookTarget.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            forcedLookRotateSpeed * Time.deltaTime
        );
    }

    // ========== 巡逻与兴趣点逻辑 ==========
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
