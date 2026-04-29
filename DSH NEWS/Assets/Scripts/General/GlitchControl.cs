using UnityEngine;
using System.Collections;
using System.Reflection;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using URPGlitch;

public class GlitchControl : MonoBehaviour
{
    [Header("Glitch1 Settings")]
    public Material glitchMaterial;
    public float noiseAmount;
    public float glitchStrength;
    public float scanLineStrength;

    [Header("Volume & Analog Glitch")]
    [Tooltip("留空则自动从当前物体获取 Volume 组件")]
    public Volume volume;
    private AnalogGlitchVolume analogGlitchVolume;

    [Range(0f, 1f)] public float scanLineJitter = 0f;
    [Range(0f, 1f)] public float verticalJump = 0f;
    [Range(0f, 1f)] public float horizontalShake = 0f;
    [Range(0f, 1f)] public float colorDrift = 0f;

    public Animator animator;
    public GlitchEventSO glitchEventSO;
    public VoidEventSO SwitchScanLineEvent;

    [Header("Glitch 音效")]
    [Tooltip("按顺序对应 Glitch1~4，留空则该类型不播放音效")]
    public AudioClip[] glitchSounds = new AudioClip[4];
    private Material featureGlitchMaterial;

    private void Start()
    {
        if (volume == null)
            volume = GetComponent<Volume>();
        if (volume != null && volume.profile != null)
            volume.profile.TryGet(out analogGlitchVolume);

        // 对话等场景 timeScale=0 时，Normal 模式 Animator 不推进，触发器后动画不播放；全屏 Glitch 依赖动画写入的标量。
        if (animator != null)
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        ResolveFeatureGlitchMaterial();

    }

    private void OnEnable()
    {
        glitchEventSO.OnEventRaised += OnGlitchEventRaised;
        SwitchScanLineEvent.OnEventRaised += OnSwitchScanLineEventRaised;
    }

    private void OnDisable()
    {
        glitchEventSO.OnEventRaised -= OnGlitchEventRaised;
        SwitchScanLineEvent.OnEventRaised -= OnSwitchScanLineEventRaised;
    }

    /// <summary>
    /// 使用 LateUpdate：确保在 Animator 本帧写入 noiseAmount 等曲线之后再同步到材质与 Volume，
    /// 避免 Update 与 Mecanim 顺序在 Player 中与编辑器不一致导致「触发成功但画面不变」。
    /// </summary>
    private void LateUpdate()
    {
        float effectiveScanLineStrength = scanLineStrength;

        if (glitchMaterial != null)
        {
            glitchMaterial.SetFloat("_NoiseAmount", noiseAmount);
            glitchMaterial.SetFloat("_GlitchStrength", glitchStrength);
            glitchMaterial.SetFloat("_ScanLineStrength", effectiveScanLineStrength);
        }
        if (featureGlitchMaterial != null && featureGlitchMaterial != glitchMaterial)
        {
            featureGlitchMaterial.SetFloat("_NoiseAmount", noiseAmount);
            featureGlitchMaterial.SetFloat("_GlitchStrength", glitchStrength);
            featureGlitchMaterial.SetFloat("_ScanLineStrength", effectiveScanLineStrength);
        }

        if (analogGlitchVolume != null)
        {
            analogGlitchVolume.scanLineJitter.value = scanLineJitter;
            analogGlitchVolume.verticalJump.value = verticalJump;
            analogGlitchVolume.horizontalShake.value = horizontalShake;
            analogGlitchVolume.colorDrift.value = colorDrift;
        }

    }

    private void ResolveFeatureGlitchMaterial()
    {
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null) return;

        ScriptableRendererData rendererData = null;
        var urpType = urpAsset.GetType();
        var srProp = urpType.GetProperty("scriptableRendererData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (srProp != null) rendererData = srProp.GetValue(urpAsset) as ScriptableRendererData;
        if (rendererData == null || rendererData.rendererFeatures == null) return;

        foreach (var feature in rendererData.rendererFeatures)
        {
            if (feature == null || feature.name != "FullScreenPassRendererFeatureGlitch")
                continue;
            var field = feature.GetType().GetField("passMaterial", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) continue;
            featureGlitchMaterial = field.GetValue(feature) as Material;
            break;
        }

    }

    private void OnGlitchEventRaised(int index, float value)
    {
        StartCoroutine(PlayGlitchWithDelay(index, value));
    }

    private IEnumerator PlayGlitchWithDelay(int index, float delay)
    {
        // 对话期间 timeScale 可能为 0，需用真实时间等待
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        if (index <= 0)
        {
            yield break;
        }

        if (animator == null)
        {
            yield break;
        }

        string triggerName = $"Glitch{index}";
        animator.SetTrigger(triggerName);
        PlayGlitchSound(index - 1);
    }

    private void PlayGlitchSound(int soundIndex)
    {
        if (glitchSounds == null || soundIndex < 0 || soundIndex >= glitchSounds.Length)
        {
            return;
        }
        var clip = glitchSounds[soundIndex];
        if (clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clip);
        }
    }

    private void OnSwitchScanLineEventRaised()
    {
        if (animator == null)
        {
            return;
        }
        animator.SetTrigger("SwitchScanLine");
    }
}
