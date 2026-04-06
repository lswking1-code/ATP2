using UnityEngine;
using UnityEngine.Video;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>用哪种方式画 Glitch 视频 UI。</summary>
public enum GlitchVideoCanvasRenderPath
{
    /// <summary>独立于相机；不会写入 Main Camera 的 Target Texture，吃不到该相机的 Global Volume，CRT Overlay 也采样不到。</summary>
    ScreenSpaceOverlay,
    /// <summary>作为指定相机（如渲染到 <c>CRT/ScreenMainTex</c> 的 Main Camera）的屏幕空间画布绘制，与 3D 同一条渲染/后处理链路，Volume 与 CRT 采样 ScreenMainTex 时会一并作用在视频上。</summary>
    ScreenSpaceCamera
}

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

    /// <summary>RawImage 所在的根 Canvas；不播片时可整层关闭，避免空白顶层 Overlay 影响全场景 UI。</summary>
    private Canvas _glitchDisplayCanvas;

    [Header("UI / Canvas")]
    [Tooltip("Scene A Test 2：选 Screen Space Camera 并拖入渲染到 ScreenMainTex 的 Main Camera，可使 Glitch 视频进入 Volume+CRT 管线。其它场景可保持 Overlay。")]
    public GlitchVideoCanvasRenderPath glitchCanvasRenderPath = GlitchVideoCanvasRenderPath.ScreenSpaceOverlay;

    [Tooltip("glitchCanvasRenderPath 为 ScreenSpaceCamera 时使用；留空则用 Camera.main（须为渲染到 ScreenMainTex 的那台相机）。")]
    public Camera compositeCamera;

    [Tooltip("Screen Space Camera 与相机前向距离；需在相机 near/far 之间（常用 1～100）。")]
    public float screenSpaceCameraPlaneDistance = 10f;

    [Tooltip("RawImage 不在任何 Canvas 下时 Unity 不会绘制 UI。为 true 时在 RawImage 所在物体上自动补 Canvas（仅当父级链上完全没有 Canvas）。")]
    public bool ensureCanvasForRawImage = true;

    [Tooltip("Canvas.sortingOrder：Overlay 时为全屏排序；Screen Space Camera 时为相对同一相机上其它画布的顺序。")]
    public int overlayCanvasSortOrder = 300;

    [Tooltip("为 true 时视频层参与点击检测并挡住下层 UI；全屏展示建议保持 false，点击会穿透到后面界面的按钮。")]
    public bool rawImageBlocksRaycasts = false;

    [Tooltip("为 true 时，未播放 glitch 视频会关闭 RawImage 的根 Canvas。仅当该 Canvas 专用于全屏视频时使用（与主界面共用 Canvas 时请关闭）。")]
    public bool disableGlitchCanvasWhenIdle = true;

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
        ApplyGlitchCanvasModeToHierarchy();

        if (_rawImage != null)
        {
            _rawImage.raycastTarget = rawImageBlocksRaycasts;
            // 全屏 Overlay + GraphicRaycaster 时，仅关 raycastTarget 仍可能被挡住；CanvasGroup.blocksRaycasts=false 才会被射线忽略并点到下层。
            ApplyVideoOverlayClickThroughPolicy();
            if (!rawImageBlocksRaycasts)
            {
                foreach (var gr in _rawImage.gameObject.GetComponentsInChildren<GraphicRaycaster>(true))
                    Destroy(gr);
            }
        }

        if (videoPlayer != null)
        {
            videoPlayer.isLooping = false;
            videoPlayer.enabled = false;
            videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            videoPlayer.skipOnDrop = false;
        }

        if (_rawImage != null)
            _glitchDisplayCanvas = _rawImage.canvas;

        if (_uiImage != null)
            _uiImage.enabled = false;

        SetGlitchDisplayVisible(false);
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

        if (_uiImage != null)
            _uiImage.enabled = false;
        SetGlitchDisplayVisible(false);

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

    /// <summary>控制全屏视频层是否拦截点击：false 时同物体上加 CanvasGroup，GraphicRaycaster 会跳过该结点及其子 Graphic。</summary>
    private void ApplyVideoOverlayClickThroughPolicy()
    {
        if (_rawImage == null)
            return;

        if (rawImageBlocksRaycasts)
        {
            var cg = _rawImage.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.blocksRaycasts = true;
                cg.interactable = true;
            }
            return;
        }

        CanvasGroup g = _rawImage.GetComponent<CanvasGroup>();
        if (g == null)
            g = _rawImage.gameObject.AddComponent<CanvasGroup>();
        g.blocksRaycasts = false;
        g.interactable = false;
    }

    /// <summary>同步 RawImage 与（可选）整层 Canvas 的显示；空闲时关掉 Canvas 可避免顶层 sort=300 的空白 Overlay 影响全场景点击。</summary>
    private void SetGlitchDisplayVisible(bool visible)
    {
        if (_rawImage != null)
            _rawImage.enabled = visible;

        if (disableGlitchCanvasWhenIdle && _glitchDisplayCanvas != null)
            _glitchDisplayCanvas.enabled = visible;
    }

    /// <summary>按 glitchCanvasRenderPath 配置 RawImage 祖先上的 Canvas（含场景里已摆好的 Canvas）。</summary>
    private void ApplyGlitchCanvasModeToHierarchy()
    {
        if (_rawImage == null)
            return;
        Canvas canvas = _rawImage.GetComponentInParent<Canvas>(true);
        if (canvas == null)
            return;

        if (glitchCanvasRenderPath == GlitchVideoCanvasRenderPath.ScreenSpaceCamera)
        {
            Camera cam = compositeCamera != null ? compositeCamera : Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("VideoGlitchPlay: ScreenSpaceCamera 模式需要 compositeCamera 或带 MainCamera 标签的相机；已回退为 Overlay。", this);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = Mathf.Max(0.01f, screenSpaceCameraPlaneDistance);
            }
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
        }

        canvas.sortingOrder = overlayCanvasSortOrder;
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

        if (host.GetComponent<CanvasScaler>() == null)
        {
            CanvasScaler scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // 展示用视频一般不需要 GraphicRaycaster；加上后 RawImage 易挡住同界面排序更低的按钮。
        if (rawImageBlocksRaycasts && host.GetComponent<GraphicRaycaster>() == null)
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

        SetGlitchDisplayVisible(true);
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
            if (_uiImage != null)
                _uiImage.enabled = false;
            SetGlitchDisplayVisible(false);
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
