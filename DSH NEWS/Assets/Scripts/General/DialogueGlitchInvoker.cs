using UnityEngine;

/// <summary>
/// 供 Dialogue System Scene Event（GameObjectUnityEvent）使用。
/// Unity 的 UnityEvent&lt;GameObject&gt; 通常只能绑定 void() 或 void(GameObject)，
/// 不能绑定带 int/float 额外参数的方法，因此用「预设资源」或「本组件上的字段」承载数值。
/// </summary>
public class DialogueGlitchInvoker : MonoBehaviour
{
    public GlitchEventSO glitchEvent;
    public GlitchEventSO glitchVideoEvent;

    [Header("方式 B：每条对话可绑不同子物体上的副本，只改下面两项")]
    [Tooltip("对应 Glitch1~4")]
    public int glitchIndex = 1;

    [Tooltip("延迟秒数")]
    public float delaySeconds = 0f;

    public void InvokeGlitch(GameObject _)
    {
        if (glitchEvent == null) return;
        glitchEvent.RaiseEvent(glitchIndex, delaySeconds);
    }

    public void InvokeGlitchVideo(GameObject _)
    {
        if (glitchVideoEvent == null) return;
        glitchVideoEvent.RaiseEvent(glitchIndex, delaySeconds);
    }

    public void InvokeGlitchBoth(GameObject _)
    {
        InvokeGlitch(_);
        InvokeGlitchVideo(_);
    }

    /// <summary>方式 A：Scene Event 中选带 Preset 的重载，Inspector 里拖入 <see cref="GlitchDialoguePreset"/>。</summary>
    public void InvokeGlitchFromPreset(GameObject _, GlitchDialoguePreset preset)
    {
        if (preset == null || glitchEvent == null) return;
        glitchEvent.RaiseEvent(preset.glitchIndex, preset.delaySeconds);
    }

    public void InvokeGlitchVideoFromPreset(GameObject _, GlitchDialoguePreset preset)
    {
        if (preset == null || glitchVideoEvent == null) return;
        glitchVideoEvent.RaiseEvent(preset.glitchIndex, preset.delaySeconds);
    }

    public void InvokeGlitchBothFromPreset(GameObject _, GlitchDialoguePreset preset)
    {
        InvokeGlitchFromPreset(_, preset);
        InvokeGlitchVideoFromPreset(_, preset);
    }
}
