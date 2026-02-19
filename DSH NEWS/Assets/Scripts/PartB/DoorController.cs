using System.Collections;
using UnityEngine;


/// 门控制器：支持绕指定轴的开/关（平滑插值）、可作为 IInteractable 使用以响应玩家交互，
/// 可播放开关音效并在 Inspector 中配置铰链（hinge）、角度与速度等参数。
/// 将此脚本挂到门对象或门的父对象上，若门的旋转中心不在根节点，可指定 hinge Transform。

public class DoorController : MonoBehaviour, IInteractable
{
    public enum Axis { X = 0, Y = 1, Z = 2 }

    [Header("Door Setting")]
    [SerializeField, Tooltip("作为旋转中心的 Transform（留空则使用当前对象）")]
    private Transform hinge;

    [SerializeField, Tooltip("关闭时相对于 hinge 的局部角度（度）")]
    private Vector3 closedEuler = Vector3.zero;

    [SerializeField, Tooltip("开启时相对于 hinge 的局部角度（度）")]
    private Vector3 openEuler = new Vector3(0f, 90f, 0f);

    [SerializeField, Tooltip("是否在开始时处于打开状态")]
    private bool startOpen = false;

    [SerializeField, Tooltip("开关过渡速度（角度插值速度，越大越快）")]
    private float openSpeed = 5f;

    [Header("Event")]
    [SerializeField, Tooltip("是否在交互后禁止重复交互（若为 true，则交互一次生效）")]
    private bool disableAfterInteract = false;

    [Header("Audio")]
    [SerializeField, Tooltip("打开时播放的音效（可选）")]
    private AudioClip openSound;

    [SerializeField, Tooltip("关闭时播放的音效（可选）")]
    private AudioClip closeSound;

    [SerializeField, Range(0f, 1f), Tooltip("音效音量")]
    private float soundVolume = 1f;

    // 内部状态
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpen;
    private bool hasInteracted = false;
    private Coroutine animCoroutine;

    private void Awake()
    {
        if (hinge == null)
            hinge = transform;

        // 记录初始局部旋转为 closedEuler 的基底：先设置为 closedEuler 再读取 localRotation
        // 为避免修改在编辑器中对象的实际旋转，我们先缓存当前局部旋转，然后计算目标旋转
        Quaternion origLocal = hinge.localRotation;

        // 计算 closedRotation（基于 closedEuler）
        closedRotation = Quaternion.Euler(closedEuler);

        // 计算 openRotation（基于 openEuler）
        openRotation = Quaternion.Euler(openEuler);

        // 还原到原始局部旋转的近似位置：若在编辑器中用户已经调整门实际角度，优先使用 startOpen 来决定初始状态
        isOpen = startOpen;
        hinge.localRotation = isOpen ? openRotation : closedRotation;

        // 若有需要，可以在此把 origLocal 用作偏移（目前使用显式 closed/open Euler）
        // 如果你希望基于当前 transform 的局部旋转偏移开关角度，可改用： closedRotation = origLocal; openRotation = origLocal * Quaternion.Euler(openEuler);
    }


    /// IInteractable 接口实现：切换门的开关状态。

    public void OnInteract()
    {
        if (hasInteracted && disableAfterInteract) return;

        Toggle();

        if (disableAfterInteract)
            hasInteracted = true;
    }


    /// 切换开/关

    public void Toggle()
    {
        if (animCoroutine != null)
            StopCoroutine(animCoroutine);

        Quaternion from = hinge.localRotation;
        Quaternion to = isOpen ? closedRotation : openRotation;
        animCoroutine = StartCoroutine(AnimateRotation(from, to, openSpeed));

        // 播放音效
        PlayToggleSound(!isOpen);

        isOpen = !isOpen;
    }


    /// 强制打开门（可在代码中调用）

    public void Open()
    {
        if (isOpen) return;
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(AnimateRotation(hinge.localRotation, openRotation, openSpeed));
        PlayToggleSound(true);
        isOpen = true;
    }


    /// 强制关闭门（可在代码中调用）

    public void Close()
    {
        if (!isOpen) return;
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(AnimateRotation(hinge.localRotation, closedRotation, openSpeed));
        PlayToggleSound(false);
        isOpen = false;
    }

    private IEnumerator AnimateRotation(Quaternion from, Quaternion to, float speed)
    {
        float t = 0f;
        // 使用角度差来控制插值时间，确保旋转速度感知一致
        float angle = Quaternion.Angle(from, to);
        if (angle <= 0.01f)
        {
            hinge.localRotation = to;
            animCoroutine = null;
            yield break;
        }

        // 使用基于角度的归一化插值时间
        while (t < 1f)
        {
            t += Time.deltaTime * speed;
            hinge.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        hinge.localRotation = to;
        animCoroutine = null;
    }

    private void PlayToggleSound(bool opening)
    {
        if (AudioManager.Instance == null) return;

        var clip = opening ? openSound : closeSound;
        if (clip != null)
            AudioManager.Instance.PlaySFX(clip, soundVolume);
    }

    // 可选：在编辑器中显示当前状态信息
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (hinge == null) hinge = transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(hinge.position, 0.05f);
    }
#endif
}