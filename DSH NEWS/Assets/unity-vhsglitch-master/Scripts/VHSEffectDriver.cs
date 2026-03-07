using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// URP 方案 A：驱动 VHS 后处理材质参数，配合 URP Blit Render Feature 使用。
/// 将此脚本挂在场景中任意物体上，将 Blit Feature 使用的同一 Material 拖到 vhsMaterial，
/// 并指定 VHS 视频。每帧会更新 _VHSTex、_yScanline、_xScanline。
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class VHSEffectDriver : MonoBehaviour
{
    [Tooltip("与 URP Blit Render Feature 中使用的 VHS 材质为同一份")]
    public Material vhsMaterial;
    [Tooltip("用于叠加的 VHS 噪点/条纹视频")]
    public VideoClip vhsClip;
    [Range(0f, 1f)]
    [Tooltip("效果强度：0 为无效果，1 为完整效果")]
    public float intensity = 0.5f;

    private float _yScanline;
    private float _xScanline;
    private VideoPlayer _player;
    private static readonly int VHSTexId = Shader.PropertyToID("_VHSTex");
    private static readonly int YScanlineId = Shader.PropertyToID("_yScanline");
    private static readonly int XScanlineId = Shader.PropertyToID("_xScanline");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private void OnEnable()
    {
        _player = GetComponent<VideoPlayer>();
        _player.isLooping = true;
        _player.renderMode = VideoRenderMode.APIOnly;
        _player.audioOutputMode = VideoAudioOutputMode.None;
        _player.clip = vhsClip;
        _player.Play();
    }

    private void LateUpdate()
    {
        if (vhsMaterial == null || _player == null || _player.texture == null)
            return;

        vhsMaterial.SetTexture(VHSTexId, _player.texture);

        _yScanline += Time.deltaTime * 0.01f;
        _xScanline -= Time.deltaTime * 0.1f;

        if (_yScanline >= 1f)
            _yScanline = Random.value;
        if (_xScanline <= 0f || Random.value < 0.05f)
            _xScanline = Random.value;

        vhsMaterial.SetFloat(YScanlineId, _yScanline);
        vhsMaterial.SetFloat(XScanlineId, _xScanline);
        vhsMaterial.SetFloat(IntensityId, intensity);
    }
}
