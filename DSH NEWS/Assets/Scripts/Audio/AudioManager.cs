using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局音频管理器（单例）。
/// 负责背景音乐和一次性音效的播放、淡入/淡出以及音量控制。
/// 支持按场景自动切换背景音乐：在 Inspector 中为不同场景指定对应的 AudioClip（按 Scene Name 或 Build Index），加载场景时自动切换（支持淡入/淡出）。
/// 建议将此组件挂在场景中的一个对象上（或在运行时主动创建），并保持为 DontDestroyOnLoad 以在场景间复用。
/// </summary>
public class AudioManager : MonoBehaviour
{
    // 单例实例
    public static AudioManager Instance { get; private set; }

    [Header("Sources")]
    [SerializeField, Tooltip("用于播放背景音乐，支持淡入/淡出与循环")]
    private AudioSource musicSource;

    [SerializeField, Tooltip("用于播放一次性音效（使用 PlayOneShot）")]
    private AudioSource sfxSource;

    [Header("Volumes")]
    [SerializeField, Range(0f, 1f), Tooltip("背景音乐总体音量")]
    private float musicVolume = 1f;

    [SerializeField, Range(0f, 1f), Tooltip("音效总体音量")]
    private float sfxVolume = 1f;

    // Scene-based music mapping
    [Header("Scene Music")]
    [SerializeField, Tooltip("启用时会在场景加载时根据映射自动切换背景音乐")]
    private bool autoSwitchOnSceneLoad = true;

    [SerializeField, Tooltip("当场景没有匹配条目时使用的后备背景音乐（可留空）")]
    private AudioClip defaultMusicClip;

    [SerializeField, Tooltip("默认淡入/淡出时长（秒），当场景条目未指定时使用")]
    private float defaultSceneFadeTime = 0.5f;

    [System.Serializable]
    private class SceneMusicEntry
    {
        [Tooltip("优先通过 Scene Name 匹配（Exact match）。如果留空，则尝试使用 Build Index 匹配。")]
        public string sceneName = "";
        [Tooltip("可选：通过 Build Index 匹配场景，-1 表示不使用。")]
        public int sceneBuildIndex = -1;
        [Tooltip("为该场景播放的 AudioClip")]
        public AudioClip clip;
        [Tooltip("如果 <= 0 则使用默认的淡入/淡出时长")]
        public float fadeTime = -1f;
        [Tooltip("播放时是否循环")]
        public bool loop = true;
    }

    [SerializeField, Tooltip("按场景映射的音乐列表，支持按 Scene Name 或 Build Index 匹配")]
    private List<SceneMusicEntry> sceneMusic = new List<SceneMusicEntry>();

    // 当前用于淡入/淡出背景音乐的协程引用
    private Coroutine musicFadeCoroutine;
    // 标记是否完成初始化（用于编辑器 OnValidate 检查）
    private bool isInitialized = false;

    /// <summary>
    /// Awake：初始化单例，确保音源存在并应用默认音量。
    /// 如果已有其他实例则销毁当前对象。
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Debug.Log("AudioManager initialized.");
        DontDestroyOnLoad(gameObject);

        EnsureAudioSources();
        ApplyVolumes();

        // 订阅场景加载事件（用于自动切换背景音乐）
        if (autoSwitchOnSceneLoad)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // 首次进入时根据当前场景播放
        if (autoSwitchOnSceneLoad)
        {
            var active = SceneManager.GetActiveScene();
            PlayMusicForScene(active);
        }
        else if (defaultMusicClip != null)
        {
            PlayMusic(defaultMusicClip, defaultSceneFadeTime, true);
        }

        isInitialized = true;
    }

    private void OnDestroy()
    {
        // 取消订阅，避免内存泄漏
        if (autoSwitchOnSceneLoad)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // 清理单例引用
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 场景加载回调：根据新场景切换背景音乐（如果启用）。
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!autoSwitchOnSceneLoad) return;
        PlayMusicForScene(scene);
    }

    /// <summary>
    /// 根据 Scene 对象查找映射并播放对应的背景音乐。
    /// 查找顺序：先按 Scene.name（非空精确匹配），再按 buildIndex（>=0 匹配），最后使用 defaultMusicClip（如果有）。
    /// 如果找到匹配且与当前 musicSource.clip 相同，则不会重复切换。
    /// </summary>
    public void PlayMusicForScene(Scene scene)
    {
        if (!scene.IsValid()) return;
        string sceneName = scene.name;
        int buildIndex = scene.buildIndex;

        SceneMusicEntry match = null;

        // 1. 按 Name 精确匹配
        for (int i = 0; i < sceneMusic.Count; i++)
        {
            var e = sceneMusic[i];
            if (!string.IsNullOrEmpty(e.sceneName) && e.sceneName == sceneName)
            {
                match = e;
                break;
            }
        }

        // 2. 按 Build Index 匹配（只有在没有通过 name 匹配时）
        if (match == null)
        {
            for (int i = 0; i < sceneMusic.Count; i++)
            {
                var e = sceneMusic[i];
                if (e.sceneBuildIndex >= 0 && e.sceneBuildIndex == buildIndex)
                {
                    match = e;
                    break;
                }
            }
        }

        // 如果找到了 Scene 映射，使用映射播放
        if (match != null && match.clip != null)
        {
            // 避免对相同 clip 做重复切换
            if (musicSource != null && musicSource.clip == match.clip && musicSource.isPlaying)
                return;

            float ft = match.fadeTime > 0f ? match.fadeTime : defaultSceneFadeTime;
            PlayMusic(match.clip, ft, match.loop);
            return;
        }

        // 使用默认音乐（如果设置）
        if (defaultMusicClip != null)
        {
            if (musicSource != null && musicSource.clip == defaultMusicClip && musicSource.isPlaying)
                return;

            PlayMusic(defaultMusicClip, defaultSceneFadeTime, true);
        }
        else
        {
            // 如果没有任何音乐，停止当前播放（淡出）
            StopMusic(defaultSceneFadeTime);
        }
    }

    /// <summary>
    /// 通过场景名（方便脚本调用）触发播放。
    /// </summary>
    public void PlayMusicForScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        SceneMusicEntry match = null;
        for (int i = 0; i < sceneMusic.Count; i++)
        {
            var e = sceneMusic[i];
            if (!string.IsNullOrEmpty(e.sceneName) && e.sceneName == sceneName)
            {
                match = e;
                break;
            }
        }

        if (match != null && match.clip != null)
        {
            float ft = match.fadeTime > 0f ? match.fadeTime : defaultSceneFadeTime;
            PlayMusic(match.clip, ft, match.loop);
            return;
        }

        if (defaultMusicClip != null)
            PlayMusic(defaultMusicClip, defaultSceneFadeTime, true);
    }

    /// <summary>
    /// 确保 musicSource 和 sfxSource 存在；若未在 Inspector 指定则动态创建子对象和 AudioSource。
    /// </summary>
    private void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            var go = new GameObject("MusicSource");
            go.transform.SetParent(transform, false);
            musicSource = go.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
        }

        if (sfxSource == null)
        {
            var go = new GameObject("SfxSource");
            go.transform.SetParent(transform, false);
            sfxSource = go.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }
    }

    /// <summary>
    /// 将序列化的音量值应用到对应的 AudioSource。
    /// 在运行时或编辑器修改值后调用以生效。
    /// </summary>
    private void ApplyVolumes()
    {
        if (musicSource != null)
            musicSource.volume = musicVolume;
        if (sfxSource != null)
            sfxSource.volume = sfxVolume;
    }

    /// <summary>
    /// 播放一次性音效（PlayOneShot），会考虑 sfxVolume 参数。
    /// clip 为 null 或 sfxSource 不存在时不执行。
    /// </summary>
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume * sfxVolume));
    }

    /// <summary>
    /// 在指定世界坐标播放 3D 一次性音效。
    /// 使用临时 AudioSource，播放完毕后自动销毁。
    /// </summary>
    public AudioSource PlaySFX3D(
        AudioClip clip,
        Vector3 worldPosition,
        float volume = 1f,
        float minDistance = 1f,
        float maxDistance = 25f,
        AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic)
    {
        if (clip == null) return null;

        var tempGo = new GameObject($"OneShot3D_{clip.name}");
        tempGo.transform.position = worldPosition;

        var source = tempGo.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = rolloffMode;
        source.minDistance = Mathf.Max(0f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, maxDistance);
        source.loop = false;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume * sfxVolume);

        source.Play();

        // 考虑 pitch 变化后，按实际播放时长销毁，避免场景残留。
        float pitchAbs = Mathf.Max(0.01f, Mathf.Abs(source.pitch));
        float lifeTime = (clip.length / pitchAbs) + 0.1f;
        Destroy(tempGo, lifeTime);

        return source;
    }

    /// <summary>
    /// 在指定挂点（Transform）位置播放 3D 一次性音效。
    /// 声源会跟随挂点移动，播放完毕后自动销毁。
    /// </summary>
    public AudioSource PlaySFX3D(
        AudioClip clip,
        Transform anchor,
        float volume = 1f,
        float minDistance = 1f,
        float maxDistance = 25f,
        AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic)
    {
        if (clip == null) return null;

        var tempGo = new GameObject($"OneShot3D_{clip.name}");
        if (anchor != null)
        {
            tempGo.transform.SetParent(anchor, false);
            tempGo.transform.localPosition = Vector3.zero;
        }

        var source = tempGo.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = rolloffMode;
        source.minDistance = Mathf.Max(0f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, maxDistance);
        source.loop = false;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume * sfxVolume);

        source.Play();

        float pitchAbs = Mathf.Max(0.01f, Mathf.Abs(source.pitch));
        float lifeTime = (clip.length / pitchAbs) + 0.1f;
        Destroy(tempGo, lifeTime);

        return source;
    }

    /// <summary>
    /// 立即设置并播放背景音乐（无淡入淡出）。
    /// </summary>
    public void PlayMusicImmediate(AudioClip clip, bool loop = true)
    {
        if (musicSource == null) return;
        musicFadeCoroutine = StopCoroutineIfRunning(musicFadeCoroutine);
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.volume = musicVolume;
        if (clip != null)
            musicSource.Play();
    }

    /// <summary>
    /// 使用淡出当前音乐、淡入新音乐的方式切换背景音乐。
    /// fadeTime 为淡入/淡出时长（秒）。
    /// </summary>
    public void PlayMusic(AudioClip clip, float fadeTime = 0.5f, bool loop = true)
    {
        if (musicSource == null)
            return;

        musicSource.loop = loop;
        musicFadeCoroutine = StopCoroutineIfRunning(musicFadeCoroutine);
        musicFadeCoroutine = StartCoroutine(FadeMusicRoutine(clip, fadeTime));
    }

    /// <summary>
    /// 停止当前背景音乐（可带淡出）。
    /// </summary>
    public void StopMusic(float fadeTime = 0.5f)
    {
        if (musicSource == null) return;
        musicFadeCoroutine = StopCoroutineIfRunning(musicFadeCoroutine);
        musicFadeCoroutine = StartCoroutine(StopMusicRoutine(fadeTime));
    }

    /// <summary>
    /// 设置背景音乐总体音量（0-1），并立即应用到 musicSource。
    /// </summary>
    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        if (musicSource != null)
            musicSource.volume = musicVolume;
    }

    /// <summary>
    /// 设置音效总体音量（0-1），并立即应用到 sfxSource。
    /// </summary>
    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        if (sfxSource != null)
            sfxSource.volume = sfxVolume;
    }

    /// <summary>
    /// 静音/取消静音（仅影响此管理器持有的音源）。
    /// </summary>
    public void SetMute(bool mute)
    {
        if (musicSource != null) musicSource.mute = mute;
        if (sfxSource != null) sfxSource.mute = mute;
    }

    /// <summary>
    /// 将当前音乐淡出并切换到 newClip 的协程实现。
    /// 若 newClip 为 null 则仅淡出并停止。
    /// </summary>
    private IEnumerator FadeMusicRoutine(AudioClip newClip, float duration)
    {
        float startVol = musicSource.volume;
        float target = 0f;

        // 淡出当前音乐
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float factor = Mathf.Clamp01(t / duration);
            // 乘以 musicVolume 以支持全局音量缩放
            musicSource.volume = Mathf.Lerp(startVol, target, factor) * musicVolume;
            yield return null;
        }

        musicSource.Stop();
        musicSource.clip = newClip;

        if (newClip == null)
        {
            musicFadeCoroutine = null;
            yield break;
        }

        musicSource.Play();

        // 淡入到设定的 musicVolume（从 0 到 1 的插值，再乘以 musicVolume）
        t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float factor = Mathf.Clamp01(t / duration);
            musicSource.volume = Mathf.Lerp(target, 1f, factor) * musicVolume;
            yield return null;
        }

        musicSource.volume = musicVolume;
        musicFadeCoroutine = null;
    }

    /// <summary>
    /// 将当前音乐淡出并停止的协程实现。
    /// </summary>
    private IEnumerator StopMusicRoutine(float duration)
    {
        float startVol = musicSource.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float factor = Mathf.Clamp01(t / duration);
            musicSource.volume = Mathf.Lerp(startVol, 0f, factor);
            yield return null;
        }

        musicSource.Stop();
        musicSource.clip = null;
        musicFadeCoroutine = null;
    }

    /// <summary>
    /// 如果传入的协程正在运行则停止它并返回 null（方便赋值/链式调用）。
    /// </summary>
    private Coroutine StopCoroutineIfRunning(Coroutine c)
    {
        if (c != null)
        {
            StopCoroutine(c);
            return null;
        }
        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!isInitialized) return;
        ApplyVolumes();
    }
#endif
}