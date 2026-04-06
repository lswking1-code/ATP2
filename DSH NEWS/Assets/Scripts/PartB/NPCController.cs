using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCController : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("可选：用于接收速度的 Animator；留空时会尝试自动获取。")]
    private Animator animator;

    [SerializeField, Tooltip("NavMeshAgent（自动获取）")]
    private NavMeshAgent agent;

    [Header("Animator")]
    [SerializeField, Tooltip("Animator 中用于接收速度的 Float 参数名。")]
    private string speedParam = "Speed";

    [Header("Patrol")]
    [SerializeField, Tooltip("按顺序的巡逻点（为空则不巡逻）。")]
    private Transform[] patrolPoints;

    [SerializeField, Tooltip("巡逻到点后的等待时间（秒），<=0 则不等待）。")]
    private float patrolWaitTime = 0f;

    [SerializeField, Tooltip("巡逻是否循环（到末尾回到开头）。")]
    private bool patrolLoop = true;

    [SerializeField, Tooltip("到达目标点认为已到达的容忍距离（米）。")]
    private float arriveThreshold = 0.2f;

    // runtime
    private int patrolIndex = 0;
    private Coroutine patrolWaitCoroutine;

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponent<Animator>();

        // 由 agent 管理位置与旋转，动画不使用 Root Motion
        if (agent != null)
            agent.updateRotation = true;

        if (animator != null)
            animator.applyRootMotion = false;
    }

    private void Start()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            patrolIndex = 0;
            agent.isStopped = false;
            agent.SetDestination(patrolPoints[patrolIndex].position);
        }
    }

    private void Update()
    {
        if (agent == null) return;

        // 将 agent 的实际速度传入 Animator（用于驱动 BlendTree / 过渡）
        if (animator != null && !string.IsNullOrEmpty(speedParam))
        {
            float speed = agent.velocity.magnitude;
            animator.SetFloat(speedParam, speed);
        }

        // 巡逻逻辑（简化）
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        if (agent.pathPending) return;

        if (!agent.hasPath || agent.remainingDistance <= arriveThreshold)
        {
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
        if (patrolLoop)
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        else if (patrolIndex < patrolPoints.Length - 1)
            patrolIndex++;

        agent.SetDestination(patrolPoints[patrolIndex].position);
    }
}