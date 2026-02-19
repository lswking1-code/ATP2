using UnityEngine;

public class GlitchControl : MonoBehaviour
{
    public Material glitchMaterial;
    public float noiseAmount;
    public float glitchStrength;
    public float scanLineStrength;

    void Update()
    {
        glitchMaterial.SetFloat("_NoiseAmount", noiseAmount);
        glitchMaterial.SetFloat("_GlitchStrength", glitchStrength);
        glitchMaterial.SetFloat("_ScanLineStrength", scanLineStrength);
    }
}
