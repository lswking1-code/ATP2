using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPC2FollowOnEvent : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("玩家 Transform。为空时会自动按 Tag=Player 查找。")]
    private Transform player;

    [SerializeField, Tooltip("NPC 的 NavMeshAgent。为空时自动获取。")]
    private NavMeshAgent agent;

    [Header("Follow")]
    [SerializeField, Min(0.1f), Tooltip("当 NPC 与玩家距离大于该值时，NPC 会追踪玩家。")]
    private float followDistance = 3f;

    [SerializeField, Min(0f), Tooltip("刷新目标点的时间间隔（秒）。")]
    private float repathInterval = 0.15f;

    [SerializeField, Tooltip("启用跟随后，是否让 NPC 始终朝向玩家（即使停下时也会缓慢转向）。")]
    private bool facePlayerWhenStopped = true;

    [SerializeField, Min(0f), Tooltip("停下时朝向玩家的旋转速度（度/秒）。")]
    private float rotateSpeed = 360f;

    [Header("Optional Override")]
    [SerializeField, Tooltip("启用跟随时会临时禁用这些行为组件（例如 NPCController），关闭跟随后恢复。")]
    private MonoBehaviour[] disableBehavioursWhenFollowing;

    private bool followEnabled;
    private float repathTimer;
    private bool[] cachedBehaviourStates;

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        CacheBehaviourStates();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
    }

    private void Update()
    {
        if (!followEnabled || player == null || agent == null) return;

        repathTimer -= Time.deltaTime;
        float sqrDist = (player.position - transform.position).sqrMagnitude;
        float followDistanceSqr = followDistance * followDistance;

        if (sqrDist > followDistanceSqr)
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

            if (facePlayerWhenStopped)
            {
                RotateTowardsPlayer();
            }
        }
    }

    /// <summary>
    /// 供外部事件调用：启用 NPC2 跟随行为。
    /// </summary>
    public void EnableFollow()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        followEnabled = true;
        repathTimer = 0f;
        SetBehavioursEnabled(false);
    }

    /// <summary>
    /// 供外部事件调用：禁用 NPC2 跟随行为并停止移动。
    /// </summary>
    public void DisableFollow()
    {
        followEnabled = false;

        if (agent != null)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }

        SetBehavioursEnabled(true);
    }

    private void RotateTowardsPlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }

    private void CacheBehaviourStates()
    {
        if (disableBehavioursWhenFollowing == null || disableBehavioursWhenFollowing.Length == 0)
        {
            cachedBehaviourStates = null;
            return;
        }

        cachedBehaviourStates = new bool[disableBehavioursWhenFollowing.Length];
        for (int i = 0; i < disableBehavioursWhenFollowing.Length; i++)
        {
            MonoBehaviour behaviour = disableBehavioursWhenFollowing[i];
            cachedBehaviourStates[i] = behaviour != null && behaviour.enabled;
        }
    }

    private void SetBehavioursEnabled(bool restoreCached)
    {
        if (disableBehavioursWhenFollowing == null || disableBehavioursWhenFollowing.Length == 0) return;

        if (cachedBehaviourStates == null || cachedBehaviourStates.Length != disableBehavioursWhenFollowing.Length)
        {
            CacheBehaviourStates();
        }

        for (int i = 0; i < disableBehavioursWhenFollowing.Length; i++)
        {
            MonoBehaviour behaviour = disableBehavioursWhenFollowing[i];
            if (behaviour == null) continue;

            behaviour.enabled = restoreCached ? cachedBehaviourStates[i] : false;
        }
    }
}
