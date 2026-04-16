using UnityEngine;

public class NPCGlobalLookEventRelay : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("玩家 Transform。留空时会自动查找 Tag=Player。")]
    private Transform player;

    [SerializeField, Tooltip("需要受控的 NPC 列表。为空时运行时自动查找场景内全部 NPCController。")]
    private NPCController[] npcs;

    private void Awake()
    {
        // 允许在 Inspector 不手动赋值玩家，运行时自动按 Tag 查找。
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
    }

    // 供 Interactable.onInteract 调用：
    // 让所有 NPC 停止自身逻辑并持续朝玩家方向旋转。
    public void EnableAllNPCsLookAtPlayer()
    {
        if (player == null)
        {
            Debug.LogWarning("[NPCGlobalLookEventRelay] Player is null. Cannot force NPCs look at player.");
            return;
        }

        EnsureNPCs();
        for (int i = 0; i < npcs.Length; i++)
        {
            if (npcs[i] == null) continue;
            npcs[i].OnPlayerInteractStart();
            npcs[i].SetForcedLookTarget(player);
        }
    }

    // 可在后续事件中调用（例如另一件物品交互后）：
    // 清除“强制看向玩家”状态，并恢复 NPC 原有行为。
    public void DisableAllNPCsLookAtPlayer()
    {
        EnsureNPCs();
        for (int i = 0; i < npcs.Length; i++)
        {
            if (npcs[i] == null) continue;
            npcs[i].ClearForcedLookTarget();
            npcs[i].OnPlayerInteractEnd();
        }
    }

    private void EnsureNPCs()
    {
        // 未手动配置列表时，首次调用自动抓取场景中全部 NPCController。
        if (npcs != null && npcs.Length > 0) return;
        npcs = Object.FindObjectsByType<NPCController>(FindObjectsSortMode.None);
    }
}
