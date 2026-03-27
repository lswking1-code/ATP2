using UnityEngine;
using UnityEngine.Video;
using System.Collections.Generic;
using UnityEngine.UI;

public class VideoGlitchPlay : MonoBehaviour
{
    public GlitchEventSO GlitchVideoEventSO;

    [Header("Video Playback")]
    [Tooltip("用于播放/切换视频的 VideoPlayer 组件")]
    public VideoPlayer videoPlayer;

    [Tooltip("按 index=1..4 顺序放置视频：1->0, 2->1, 3->2, 4->3")]
    public List<VideoClip> videoClips = new List<VideoClip>();

    // 用于避免多次触发时回调串台（Prepare/Play 异步）
    private VideoClip _pendingClip;

    // 用于显示视频的 UI 组件（通常和 VideoPlayer 在同一个物体上）
    private RawImage _rawImage;
    private Image _uiImage;

    [Header("End Behavior")]
    [Tooltip("播放结束后是否清空 targetTexture（避免画面停留在最后一帧）")]
    public bool clearTargetTextureOnEnd = false;

    [Tooltip("清空 targetTexture 的颜色")]
    public Color clearColorOnEnd = Color.black;

    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        _rawImage = GetComponent<RawImage>();
        _uiImage = GetComponent<Image>();

        if (videoPlayer != null)
        {
            // 默认不播放：仅在收到 GlitchEventSO 时启用并播放
            videoPlayer.isLooping = false;
            videoPlayer.enabled = false;
        }

        // 默认不显示视频：只有收到 glitch 时才打开
        if (_rawImage != null)
            _rawImage.enabled = false;
        if (_uiImage != null)
            _uiImage.enabled = false;
    }
    
    private void OnEnable()
    {
        if (GlitchVideoEventSO != null)
            GlitchVideoEventSO.OnEventRaised += OnGlitchVideoEventRaised;

        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.loopPointReached += OnVideoLoopPointReached;
        }
    }

    private void OnDisable()
    {
        if (GlitchVideoEventSO != null)
            GlitchVideoEventSO.OnEventRaised -= OnGlitchVideoEventRaised;

        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.loopPointReached -= OnVideoLoopPointReached;
        }
    }

    private void OnVideoPrepared(VideoPlayer vp)
    {
        // 只有当 prepare 完成的 clip 仍然是我们刚才要播放的那一个，才开始播放
        if (vp != null && vp.clip == _pendingClip && _pendingClip != null)
        {
            vp.Play();
        }
    }

    private void OnVideoLoopPointReached(VideoPlayer vp)
    {
        // 播放结束后关闭 VideoPlayer（满足：只在播放时启用）
        if (vp != null)
        {
            // 关闭 UI 显示：避免渲染纹理停留/被清空后变成黑屏
            if (_rawImage != null)
                _rawImage.enabled = false;
            if (_uiImage != null)
                _uiImage.enabled = false;

            vp.Stop();

            if (clearTargetTextureOnEnd && vp.targetTexture != null)
                ClearRenderTexture(vp.targetTexture, clearColorOnEnd);

            vp.clip = null; // 清空 clip：避免你看到“播放结束了但 clip 仍然存在”
            vp.enabled = false;
            _pendingClip = null;
        }
    }

    private void ClearRenderTexture(RenderTexture rt, Color color)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, color);
        RenderTexture.active = prev;
    }

    private void OnGlitchVideoEventRaised(int index)
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer == null)
        {
            Debug.LogWarning("VideoGlitchPlay: videoPlayer 未设置/不存在。", this);
            return;
        }

        if (videoClips == null || videoClips.Count == 0)
        {
            Debug.LogWarning("VideoGlitchPlay: videoClips 为空，请在 Inspector 按顺序填充。", this);
            return;
        }

        // 按你的约定：index 取值主要为 1-4
        int listIndex = index - 1;
        if (listIndex < 0 || listIndex >= videoClips.Count)
        {
            Debug.LogWarning($"VideoGlitchPlay: index={index} 对应的 listIndex={listIndex} 越界。videoClips.Count={videoClips.Count}", this);
            return;
        }

        VideoClip clip = videoClips[listIndex];
        if (clip == null)
        {
            Debug.LogWarning($"VideoGlitchPlay: videoClips[{listIndex}] 为空（index={index}）。", this);
            return;
        }

        // 停止旧视频并从头开始播放新视频
        _pendingClip = clip;

        // 打开 UI 显示
        if (_rawImage != null)
            _rawImage.enabled = true;
        if (_uiImage != null)
            _uiImage.enabled = true;

        videoPlayer.enabled = true; // 仅播放期间启用
        videoPlayer.Stop();
        videoPlayer.clip = clip;
        videoPlayer.time = 0.0;
        videoPlayer.isLooping = false;

        // Prepare->Play：更稳定地触发 loopPointReached 等事件
        videoPlayer.Prepare();
        if (videoPlayer.isPrepared)
            videoPlayer.Play();
    }
}
