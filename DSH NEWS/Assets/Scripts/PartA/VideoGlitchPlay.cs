using UnityEngine;
using UnityEngine.Video;
using System.Collections;
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

    private Coroutine _playVideoRoutine;
    private UnityEngine.Video.VideoPlayer.EventHandler _prepareHandler;
    private VideoClip _clipAtPlayStart;

    private RawImage _rawImage;
    private Image _uiImage;

    [Header("UI / Canvas")]
    [Tooltip("RawImage 不在任何 Canvas 下时 Unity 不会绘制 UI。为 true 时在 RawImage 所在物体上自动补 Canvas（仅当父级链上完全没有 Canvas）。")]
    public bool ensureCanvasForRawImage = true;

    [Tooltip("自动创建 Canvas 时使用，越大越靠前（仅当上面选项生效）。")]
    public int overlayCanvasSortOrder = 300;

    [Header("End Behavior")]
    [Tooltip("播放结束后是否清空 targetTexture（避免画面停留在最后一帧）")]
    public bool clearTargetTextureOnEnd = false;

    [Tooltip("清空 targetTexture 的颜色")]
    public Color clearColorOnEnd = Color.black;

    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        // 优先使用「本物体上已挂 RenderTexture」的 RawImage；否则在子级里找（常见：VP/脚本在父物体，RawImage 在子物体）
        _rawImage = ResolveRawImageForRenderTextureTarget();
        _uiImage = GetComponent<Image>();

        EnsureRawImageUnderCanvas();

        if (videoPlayer != null)
        {
            videoPlayer.isLooping = false;
            videoPlayer.enabled = false;
            videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            videoPlayer.skipOnDrop = false;
        }

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
            videoPlayer.loopPointReached += OnVideoLoopPointReached;
    }

    private void OnDisable()
    {
        DetachPrepareHandler();
        if (GlitchVideoEventSO != null)
            GlitchVideoEventSO.OnEventRaised -= OnGlitchVideoEventRaised;

        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoLoopPointReached;
    }

    private void DetachPrepareHandler()
    {
        if (videoPlayer != null && _prepareHandler != null)
        {
            videoPlayer.prepareCompleted -= _prepareHandler;
            _prepareHandler = null;
        }
    }

    private void OnVideoLoopPointReached(VideoPlayer vp)
    {
        if (vp == null || vp != videoPlayer)
            return;

        if (_clipAtPlayStart == null || vp.clip != _clipAtPlayStart)
            return;

        double len = vp.length;
        if (len > 0.05d && vp.time + 0.12d < len)
            return;

        // 防止迟到/错步的 loop 回调在下一段刚开时误清理（time 与 clip 可能短暂不一致）
        long fc = (long)vp.frameCount;
        if (fc > 1)
        {
            long fr = vp.frame;
            if (fr < fc - 1L)
                return;
        }

        _clipAtPlayStart = null;

        if (_rawImage != null)
            _rawImage.enabled = false;
        if (_uiImage != null)
            _uiImage.enabled = false;

        vp.Stop();

        if (clearTargetTextureOnEnd && vp.targetTexture != null)
            ClearRenderTexture(vp.targetTexture, clearColorOnEnd);

        vp.clip = null;
        vp.enabled = false;
    }

    private void ClearRenderTexture(RenderTexture rt, Color color)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, color);
        RenderTexture.active = prev;
    }

    /// <summary>找到应与 VideoPlayer 共用 RT 的 RawImage（Inspector 里通常为子物体）。</summary>
    private RawImage ResolveRawImageForRenderTextureTarget()
    {
        var self = GetComponent<RawImage>();
        if (self != null && self.texture is RenderTexture)
            return self;

        var all = GetComponentsInChildren<RawImage>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].texture is RenderTexture)
                return all[i];
        }

        return self != null ? self : (all.Length > 0 ? all[0] : null);
    }

    /// <summary>无父级 Canvas 时 RawImage 不会参与屏幕绘制，必要时在 RawImage 所在物体上补 Canvas。</summary>
    private void EnsureRawImageUnderCanvas()
    {
        if (_rawImage == null || !ensureCanvasForRawImage)
            return;

        if (_rawImage.GetComponentInParent<Canvas>(true) != null)
            return;

        GameObject host = _rawImage.gameObject;
        Canvas canvas = host.GetComponent<Canvas>();
        if (canvas == null)
            canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = overlayCanvasSortOrder;

        if (host.GetComponent<CanvasScaler>() == null)
        {
            CanvasScaler scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (host.GetComponent<GraphicRaycaster>() == null)
            host.AddComponent<GraphicRaycaster>();
    }

    /// <summary>
    /// 若 Raw Image 已指定 RenderTexture（例如「New Render Texture」），则视频必须输出到该 RT。
    /// 仅 Material Override 不会更新 Raw Image 上的贴图，第二段尤其容易表现为无画面。
    /// </summary>
    private void SyncVideoPlayerToRawImageRenderTexture()
    {
        if (videoPlayer == null || _rawImage == null)
            return;
        if (_rawImage.texture is not RenderTexture rt)
            return;

        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetMaterialRenderer = null;
        videoPlayer.targetMaterialProperty = null;
        videoPlayer.targetTexture = rt;
        _rawImage.texture = rt;
    }

    /// <summary> MaterialOverride 换第二段 clip 后，部分环境下目标材质槽需重新触发表层刷新。</summary>
    private void RefreshMaterialOverrideTextureSlot()
    {
        if (videoPlayer == null || videoPlayer.renderMode != VideoRenderMode.MaterialOverride)
            return;
        var rend = videoPlayer.targetMaterialRenderer;
        string prop = videoPlayer.targetMaterialProperty;
        if (rend == null || string.IsNullOrEmpty(prop))
            return;
        Material m = rend.sharedMaterial;
        if (m == null || !m.HasProperty(prop))
            return;
        Texture t = m.GetTexture(prop);
        m.SetTexture(prop, t);
    }

    /// <summary> 当 VideoPlayer 为 Render Texture 模式时，把 RT 绑到 RawImage。</summary>
    private void SyncUiWithTargetTexture()
    {
        if (videoPlayer == null || _rawImage == null)
            return;
        if (videoPlayer.renderMode != VideoRenderMode.RenderTexture)
            return;
        var rt = videoPlayer.targetTexture;
        if (rt == null)
            return;
        _rawImage.texture = rt;
        var c = _rawImage.color;
        if (c.a < 1f / 255f)
            _rawImage.color = new Color(c.r, c.g, c.b, 1f);
    }

    private void OnGlitchVideoEventRaised(int index, float value)
    {
        DetachPrepareHandler();
        if (_playVideoRoutine != null)
            StopCoroutine(_playVideoRoutine);
        _clipAtPlayStart = null;
        _playVideoRoutine = StartCoroutine(PlayVideoWithDelay(index, value));
    }

    private IEnumerator PlayVideoWithDelay(int index, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer == null)
        {
            Debug.LogWarning("VideoGlitchPlay: videoPlayer 未设置/不存在。", this);
            _playVideoRoutine = null;
            yield break;
        }

        if (videoClips == null || videoClips.Count == 0)
        {
            Debug.LogWarning("VideoGlitchPlay: videoClips 为空，请在 Inspector 按顺序填充。", this);
            _playVideoRoutine = null;
            yield break;
        }

        int listIndex = index - 1;
        if (listIndex < 0 || listIndex >= videoClips.Count)
        {
            Debug.LogWarning($"VideoGlitchPlay: index={index} 对应的 listIndex={listIndex} 越界。videoClips.Count={videoClips.Count}", this);
            _playVideoRoutine = null;
            yield break;
        }

        VideoClip clip = videoClips[listIndex];
        if (clip == null)
        {
            Debug.LogWarning($"VideoGlitchPlay: videoClips[{listIndex}] 为空（index={index}）。", this);
            _playVideoRoutine = null;
            yield break;
        }

        if (_rawImage != null)
            _rawImage.enabled = true;
        if (_uiImage != null)
            _uiImage.enabled = true;

        videoPlayer.enabled = true;
        _clipAtPlayStart = null;

        videoPlayer.Stop();
        videoPlayer.clip = null;
        yield return null;

        videoPlayer.clip = clip;
        videoPlayer.time = 0.0;
        videoPlayer.isLooping = false;
        // 给 WMF/解码器一帧释放上一段资源，再 Prepare（利于第二段）
        yield return null;

        // 须在 Stop/clip 指派之后再绑定输出：部分环境下 Stop 会重置 targetTexture，先 Sync 再 Stop 会导致 Prepare 无输出或黑屏
        SyncVideoPlayerToRawImageRenderTexture();

        bool prepareSucceeded = false;
        _prepareHandler = _ => { prepareSucceeded = true; };
        videoPlayer.prepareCompleted += _prepareHandler;
        try
        {
            videoPlayer.Prepare();
            const float prepareTimeout = 15f;
            float waited = 0f;
            while (!prepareSucceeded && waited < prepareTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        finally
        {
            DetachPrepareHandler();
        }

        if (!prepareSucceeded || !videoPlayer.isPrepared)
        {
            Debug.LogWarning("VideoGlitchPlay: VideoPlayer.Prepare 未完成或超时，未开始播放。", this);
            _playVideoRoutine = null;
            yield break;
        }

        SyncUiWithTargetTexture();
        RefreshMaterialOverrideTextureSlot();
        videoPlayer.waitForFirstFrame = true;

        _clipAtPlayStart = clip;
        videoPlayer.Play();

        yield return null;
        RefreshMaterialOverrideTextureSlot();
        SyncUiWithTargetTexture();

        _playVideoRoutine = null;
    }
}
