using System.Collections;
using UnityEngine;
using UnityEngine.AI; // 必须引用导航命名空间

/// <summary>
/// 简单的 NPC 巡逻寻路器：按 Waypoints 顺序导航，支持到点等待、是否循环、Inspector 注释与可视化。
/// </summary>
public class NPCPathfinder : MonoBehaviour
{
    [Header("巡逻点")]
    [SerializeField, Tooltip("将创建的路标点拖进这个数组（Inspector 可见，已封装为私有）。")]
    private Transform[] waypoints; // Inspector 可编辑，但对外为私有，避免外部直接修改

    [Header("巡逻设置")]
    [SerializeField, Tooltip("到达目标点的阈值（单位：米），小于等于 stoppingDistance + 本值 视为到达")]
    private float arriveThreshold = 0.1f;

    [SerializeField, Tooltip("到达路点后等待的秒数（<=0 则不等待）")]
    private float waitAtPoint = 0f;

    [SerializeField, Tooltip("是否在到最后一个点后循环回到开头")]
    private bool loop = true;

    [SerializeField, Tooltip("是否在 Start 时自动开始巡逻")]
    private bool autoStart = true;

    // 状态字段（不在 Inspector 中显示）
    private int currentPointIndex = 0;
    private NavMeshAgent agent;
    private bool isWaiting = false;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogWarning($"{nameof(NPCPathfinder)} requires a NavMeshAgent on the same GameObject.");
            enabled = false;
            return;
        }

        // 防御式编程：避免 NullReference 或空数组访问
        if (waypoints == null || waypoints.Length == 0)
        {
            // 不直接禁用脚本，允许在运行时通过脚本设置 waypoints
            if (autoStart)
                Debug.LogWarning($"{nameof(NPCPathfinder)}: 未在 Inspector 中配置任何 Waypoints。");
            return;
        }

        if (autoStart)
            GoToNextWaypoint();
    }

    private void Update()
    {
        if (agent == null) return;
        if (isWaiting) return;

        // 等待路径计算完成且接近目标点（使用 stoppingDistance + arriveThreshold）
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + arriveThreshold)
        {
            // 如果需要等待，启动等待协程；否则立即前往下一个点
            if (waitAtPoint > 0f)
                StartCoroutine(WaitAndProceed(waitAtPoint));
            else
                GoToNextWaypoint();
        }
    }

    /// <summary>
    /// 将导航目标设置为当前索引对应的路点（若存在）。
    /// 索引更新策略：设置目标后将索引推进到下一个（便于下一次调用直接作用于下一个点）。
    /// </summary>
    private void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        if (currentPointIndex < 0 || currentPointIndex >= waypoints.Length) currentPointIndex = 0;

        var target = waypoints[currentPointIndex];
        if (target == null)
        {
            Debug.LogWarning($"{nameof(NPCPathfinder)}: waypoints 包含 null 元素，索引 {currentPointIndex}。");
            // 尝试跳过 null 元素
            AdvanceIndex();
            return;
        }

        agent.destination = target.position;

        // 索引推进策略：若不循环且已到最后一个点，则停止在最后一个点
        if (loop)
            currentPointIndex = (currentPointIndex + 1) % waypoints.Length;
        else
        {
            if (currentPointIndex < waypoints.Length - 1)
                currentPointIndex++;
            // 否则保持在末尾索引（不再推进）
        }
    }

    /// <summary>
    /// 在到达点后等待指定秒数再前往下一个点。
    /// </summary>
    private IEnumerator WaitAndProceed(float seconds)
    {
        isWaiting = true;
        yield return new WaitForSeconds(seconds);
        isWaiting = false;
        GoToNextWaypoint();
    }

    /// <summary>
    /// 跳过当前索引（用于遇到 null 路点时）
    /// </summary>
    private void AdvanceIndex()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        if (loop)
            currentPointIndex = (currentPointIndex + 1) % waypoints.Length;
        else
        {
            if (currentPointIndex < waypoints.Length - 1)// 仅在未到最后一个点时推进索引
                currentPointIndex++;
        }
    }

    // 在 Scene 视图绘制路点与连线，便于调试（仅在编辑器与选中时生效）
    private void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            var t = waypoints[i];
            if (t == null) continue;
            Gizmos.DrawSphere(t.position, 0.2f);

            // 绘制连线（当前点 -> 下一个点）
            var nextIndex = (i + 1) % waypoints.Length;
            if (nextIndex < waypoints.Length && waypoints[nextIndex] != null)
                Gizmos.DrawLine(t.position, waypoints[nextIndex].position);
        }
    }

    IEnumerator WaitAndGo()
    {
        yield return new WaitForSeconds(2f);
        GoToNextWaypoint();
    }
}