using UnityEngine;

public class GlitchControl : MonoBehaviour
{
    public Material glitchMaterial;
    public float noiseAmount;
    public float glitchStrength;
    public float scanLineStrength;
    public Animator animator;
    public GlitchEventSO glitchEventSO;

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
        glitchMaterial.SetFloat("_NoiseAmount", noiseAmount);
        glitchMaterial.SetFloat("_GlitchStrength", glitchStrength);
        glitchMaterial.SetFloat("_ScanLineStrength", scanLineStrength);
    }

    private void OnGlitchEventRaised(int index)
    {
        switch (index)
        {
            case 1:
                animator.SetTrigger("Glitch1");
                break;
            case 2:
                animator.SetTrigger("Glitch2");
                break;
            case 3:
                animator.SetTrigger("Glitch3");
                break;
            case 4:
                animator.SetTrigger("Glitch4");
                break;
        }
    }
}
