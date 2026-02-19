using UnityEngine;
using UnityEngine.Events;


/// 通用可交互物体组件。
/// 在 Inspector 中通过 UnityEvent 指定交互时触发的行为（比如触发动画、切换状态、发送事件等）。
/// 可选播放音效、交互后销毁或禁用重复交互，并可通过 GetPrompt() 获取交互提示文本供 UI 显示。

public class Interactable : MonoBehaviour, IInteractable
{
    [Header("Tips")]
    [SerializeField, Tooltip("交互提示文本（UI 可通过 GetPrompt() 获取并显示）")]
    private string prompt = "Press E To Have A Look";

    [SerializeField, Tooltip("是否允许显示提示（仅供 UI 使用）")]
    private bool showPrompt = true;

    [Header("Event")]
    [SerializeField, Tooltip("交互时触发的事件（可在 Inspector 里拖入其它组件方法）")]
    private UnityEvent onInteract;

    [SerializeField, Tooltip("交互后是否销毁该物体")]
    private bool destroyOnInteract = false;

    [SerializeField, Tooltip("交互后是否禁止重复交互（若为 false，可重复触发 onInteract）")]
    private bool disableAfterInteract = true;

    [Header("Audio")]
    [SerializeField, Tooltip("交互时播放的音频（可选）")]
    private AudioClip interactSound;

    [SerializeField, Range(0f, 1f)]
    private float soundVolume = 1f;

    private bool hasInteracted = false;


    /// IInteractable 接口实现。由 PlayerController 调用。

    public void OnInteract()
    {
        if (hasInteracted && disableAfterInteract) return;

        onInteract?.Invoke();// 触发事件（如果有订阅）

        if (interactSound != null)
           AudioManager.Instance.PlaySFX(interactSound, soundVolume);

        if (destroyOnInteract)
        {
            Destroy(gameObject);
            return;
        }

        if (disableAfterInteract)
            hasInteracted = true;

        // 可选：在交互后更新提示状态（如果需要 UI 刷新提示显示）
    }


    /// 返回应显示的交互提示（如果不应显示则返回 null）。
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
