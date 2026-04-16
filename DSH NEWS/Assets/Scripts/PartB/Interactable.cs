using UnityEngine;
using UnityEngine.Events;
using PixelCrushers.DialogueSystem;


/// 通用可交互物体组件。
/// 在 Inspector 中通过 UnityEvent 指定交互时触发的行为
/// （例如播放动画、切换状态、发送事件等）。
/// 可以选择播放音效、在交互后销毁自身或禁止再次交互，
/// 并通过 GetPrompt() 提供交互提示文本供 UI 显示。

public class Interactable : MonoBehaviour, IInteractable
{
    [Header("提示")]
    [SerializeField, Tooltip("交互提示文本（UI 可通过 GetPrompt() 获取并显示）。")]
    private string prompt = "Press E To Have A Look";

    [SerializeField, Tooltip("是否允许显示提示（仅供 UI 使用）。")]
    private bool showPrompt = true;

    [Header("事件")]
    [SerializeField, Tooltip("交互时触发的事件（可在 Inspector 中拖入其他组件的方法）。")]
    private UnityEvent onInteract;

    [SerializeField, Tooltip("交互后是否销毁该物体。")]
    private bool destroyOnInteract = false;

    [SerializeField, Tooltip("交互后是否禁止重复交互（若为 false，可重复触发 onInteract）。")]
    private bool disableAfterInteract = true;

    [Header("对话系统")]
    [SerializeField, Tooltip("交互时是否自动触发 Pixel Crushers Dialogue System 的对话。")]
    private bool startDialogueOnInteract = false;

    [SerializeField, Tooltip("要播放的 Conversation Title（需与 Dialogue Database 中的会话标题一致）。")]
    private string conversationTitle;

    [SerializeField, Tooltip("对话中的 Actor（通常为玩家）。留空时使用 Camera.main 的 Transform。")]
    private Transform dialogueActor;

    [SerializeField, Tooltip("对话中的 Conversant（通常为当前物体/NPC）。留空时使用当前物体的 Transform。")]
    private Transform dialogueConversant;

    [Header("音频")]
    [SerializeField, Tooltip("交互时播放的音效（可选）。")]
    private AudioClip interactSound;

    [SerializeField, Range(0f, 1f)]
    private float soundVolume = 1f;

    private bool hasInteracted = false;


    /// IInteractable 接口实现，由 PlayerController 调用。

    public void OnInteract()
    {
        if (hasInteracted && disableAfterInteract) return;

        onInteract?.Invoke(); // 触发事件（如果有监听）

        TryStartDialogue();

        if (interactSound != null)
           AudioManager.Instance.PlaySFX(interactSound, soundVolume);

        if (destroyOnInteract)
        {
            Destroy(gameObject);
            return;
        }

        if (disableAfterInteract)
            hasInteracted = true;

        // 可选：交互后更新提示状态（如果需要 UI 刷新提示显示）。
    }

    private void TryStartDialogue()
    {
        if (!startDialogueOnInteract) return;

        if (string.IsNullOrWhiteSpace(conversationTitle))
        {
            Debug.LogWarning($"[{name}] 已勾选 startDialogueOnInteract，但 conversationTitle 为空。");
            return;
        }

        if (DialogueManager.IsConversationActive) return;

        Transform actor = dialogueActor != null ? dialogueActor : (Camera.main != null ? Camera.main.transform : null);
        Transform conversant = dialogueConversant != null ? dialogueConversant : transform;

        DialogueManager.StartConversation(conversationTitle, actor, conversant);
    }


    /// 返回应显示的交互提示文本；如果不应显示则返回 null。
    /// PlayerController 或 UI 管理器可调用此方法在屏幕上显示提示。

    public string GetPrompt()
    {
        if (!showPrompt) return null;
        if (hasInteracted && disableAfterInteract) return null;
        return prompt;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
}
