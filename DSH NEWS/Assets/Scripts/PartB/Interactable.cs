using UnityEngine;
using UnityEngine.Events;
using PixelCrushers.DialogueSystem;


/// 通用可交互物体组件 / Generic interactable object component.
/// 在 Inspector 中通过 UnityEvent 指定交互时触发的行为 /
/// Configure interaction behaviors via UnityEvent in the Inspector
/// （例如播放动画、切换状态、发送事件等）/
/// (e.g., play animations, toggle states, send events).
/// 可以选择播放音效、在交互后销毁自身或禁止再次交互 /
/// Optionally play SFX, destroy itself after interaction, or disable re-interaction.

public class Interactable : MonoBehaviour, IInteractable
{
    [Header("事件 / Events")]
    [SerializeField, Tooltip("交互时触发的事件（可在 Inspector 中拖入其他组件的方法）/ Event invoked on interaction (you can drag methods from other components in the Inspector).")]
    private UnityEvent onInteract;

    [SerializeField, Tooltip("交互后是否销毁该物体 / Whether to destroy this object after interaction.")]
    private bool destroyOnInteract = false;

    [SerializeField, Tooltip("交互后是否禁止重复交互（若为 false，可重复触发 onInteract）/ Whether to block repeated interaction after first use (if false, onInteract can be triggered repeatedly).")]
    private bool disableAfterInteract = true;

    [Header("对话系统 / Dialogue System")]
    [SerializeField, Tooltip("交互时是否自动触发 Pixel Crushers Dialogue System 的对话 / Whether to automatically start a Pixel Crushers Dialogue System conversation on interaction.")]
    private bool startDialogueOnInteract = false;

    [SerializeField, Tooltip("要播放的 Conversation Title（需与 Dialogue Database 中的会话标题一致）/ Conversation Title to play (must match the title in the Dialogue Database).")]
    private string conversationTitle;

    [SerializeField, Tooltip("对话中的 Actor（通常为玩家）。留空时使用 Camera.main 的 Transform / Actor in the conversation (usually the player). If empty, Camera.main transform is used.")]
    private Transform dialogueActor;

    [SerializeField, Tooltip("对话中的 Conversant（通常为当前物体/NPC）。留空时使用当前物体的 Transform / Conversant in the conversation (usually this object/NPC). If empty, this object's transform is used.")]
    private Transform dialogueConversant;

    [Header("音频 / Audio")]
    [SerializeField, Tooltip("交互时播放的音效（可选）/ Optional sound effect played on interaction.")]
    private AudioClip interactSound;

    [SerializeField, Range(0f, 1f)]
    private float soundVolume = 1f;

    private bool hasInteracted = false;


    /// IInteractable 接口实现，由 PlayerController 调用 /
    /// IInteractable implementation, called by PlayerController.

    public void OnInteract()
    {
        if (hasInteracted && disableAfterInteract) return;

        onInteract?.Invoke(); // 触发事件（如果有监听）/ Invoke event (if there are listeners).

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

        // 可选：交互后更新提示状态（如果需要 UI 刷新提示显示）/
        // Optional: refresh prompt state after interaction (if UI hint refresh is needed).
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
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
}
