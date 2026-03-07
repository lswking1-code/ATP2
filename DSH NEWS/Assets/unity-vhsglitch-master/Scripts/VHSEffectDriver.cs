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
    [Range(0.1f, 5f)]
    [Tooltip("噪点视频播放速度：1 为正常速度，越大越快，越小越慢")]
    public float noisePlaybackSpeed = 1f;
    [Range(0f, 1f)]
    [Tooltip("扫描线随机闪动概率：0 为不随机闪（几乎无闪烁），越大闪得越频繁")]
    public float scanlineGlitchChance = 0.05f;

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
        _player.playbackSpeed = noisePlaybackSpeed;
        _player.Play();
    }

    private void LateUpdate()
    {
        if (vhsMaterial == null || _player == null || _player.texture == null)
            return;

        vhsMaterial.SetTexture(VHSTexId, _player.texture);
        _player.playbackSpeed = noisePlaybackSpeed;

        _yScanline += Time.deltaTime * 0.01f;
        _xScanline -= Time.deltaTime * 0.1f;

        if (_yScanline >= 1f)
            _yScanline = Random.value;
        if (_xScanline <= 0f || (scanlineGlitchChance > 0f && Random.value < scanlineGlitchChance))
            _xScanline = Random.value;

        vhsMaterial.SetFloat(YScanlineId, _yScanline);
        vhsMaterial.SetFloat(XScanlineId, _xScanline);
        vhsMaterial.SetFloat(IntensityId, intensity);
    }
}
