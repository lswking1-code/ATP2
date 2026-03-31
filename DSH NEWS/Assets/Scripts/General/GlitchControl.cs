using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
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

    [Header("Glitch 音效")]
    [Tooltip("按顺序对应 Glitch1~4，留空则该类型不播放音效")]
    public AudioClip[] glitchSounds = new AudioClip[4];

    private void Start()
    {
        if (volume == null)
            volume = GetComponent<Volume>();
        if (volume != null && volume.profile != null)
            volume.profile.TryGet(out analogGlitchVolume);
    }

    private void OnEnable()
    {
        glitchEventSO.OnEventRaised += OnGlitchEventRaised;
    }

    private void OnDisable()
    {
        glitchEventSO.OnEventRaised -= OnGlitchEventRaised;
    }

    void Update()
    {
        if (glitchMaterial != null)
        {
            glitchMaterial.SetFloat("_NoiseAmount", noiseAmount);
            glitchMaterial.SetFloat("_GlitchStrength", glitchStrength);
            glitchMaterial.SetFloat("_ScanLineStrength", scanLineStrength);
        }

        if (analogGlitchVolume != null)
        {
            analogGlitchVolume.scanLineJitter.value = scanLineJitter;
            analogGlitchVolume.verticalJump.value = verticalJump;
            analogGlitchVolume.horizontalShake.value = horizontalShake;
            analogGlitchVolume.colorDrift.value = colorDrift;
        }
    }

    private void OnGlitchEventRaised(int index, float value)
    {
        StartCoroutine(PlayGlitchWithDelay(index, value));
    }

    private IEnumerator PlayGlitchWithDelay(int index, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        switch (index)
        {
            case 1:
                animator.SetTrigger("Glitch1");
                PlayGlitchSound(0);
                break;
            case 2:
                animator.SetTrigger("Glitch2");
                PlayGlitchSound(1);
                break;
            case 3:
                animator.SetTrigger("Glitch3");
                PlayGlitchSound(2);
                break;
            case 4:
                animator.SetTrigger("Glitch4");
                PlayGlitchSound(3);
                break;
        }
    }

    private void PlayGlitchSound(int soundIndex)
    {
        if (glitchSounds == null || soundIndex < 0 || soundIndex >= glitchSounds.Length)
            return;
        var clip = glitchSounds[soundIndex];
        if (clip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clip);
    }
}
