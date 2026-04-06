using UnityEngine;

[CreateAssetMenu(fileName = "GlitchDialoguePreset", menuName = "Scriptable Objects/Glitch Dialogue Preset")]
public class GlitchDialoguePreset : ScriptableObject
{
    [Tooltip("对应 GlitchControl / VideoGlitchPlay：通常为 1~4")]
    public int glitchIndex = 1;

    [Tooltip("延迟秒数（传给 GlitchEventSO 的 value）")]
    public float delaySeconds = 0f;
}
