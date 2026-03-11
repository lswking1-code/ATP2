using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局音频管理器（单例）。
/// 负责背景音乐和一次性音效的播放、淡入/淡出以及音量控制。
/// 同时集成了简单的播放列表（Playlist）功能：可以在 Inspector 指定曲目列表或从 Resources 加载，支持按索引/名称播放、随机播放与顺序切换。
/// 另外增加了按场景自动切换背景音乐的功能：在 Inspector 中为不同场景指定对应的 AudioClip（按 Scene Name 或 Build Index），加载场景时自动切换（支持淡入/淡出）。
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

    // Playlist 配置（集成）
    [Header("Playlist")]
    [SerializeField, Tooltip("在 Inspector 指定要播放的音乐列表（优先于 Resources 加载）")]
    private AudioClip[] playlist;

    [SerializeField, Tooltip("如果启用，则从 ResourcesPath 加载音频（会覆盖 Inspector 中的 playlist）")]
    private bool loadFromResources = false;

    [SerializeField, Tooltip("Resources 下的路径，例如 Resources/music/ 下的音频，Path = \"music\"")]
    private string resourcesPath = "music";

    [SerializeField, Tooltip("启用时 Awake 后会自动播放 playlist 的第一首（如果存在）")]
    private bool playPlaylistOnAwake = false;

    [SerializeField, Tooltip("播放时的淡入/淡出时长（秒），仅供 Playlist 方法默认使用")]
    private float playlistFadeTime = 0.5f;

    [SerializeField, Tooltip("Playlist 的播放是否循环（Next 到末尾回到开头）")]
    private bool playlistLoop = true;

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

    // Playlist 状态
    private int currentIndex = -1;

    /// <summary>
    /// Awake：初始化单例，确保音源存在、加载 playlist（如果需要）并应用默认音量。
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

        // 如果需要从 Resources 加载 playlist
        if (loadFromResources)
        {
            var clips = Resources.LoadAll<AudioClip>(resourcesPath);
            if (clips != null && clips.Length > 0)
            {
                playlist = clips;
            }
        }

        // 订阅场景加载事件（用于自动切换背景音乐）
        if (autoSwitchOnSceneLoad)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // 自动播放 playlist 第一首（如果设置并且有曲目）
        if (playPlaylistOnAwake && playlist != null && playlist.Length > 0)
        {
            PlayByIndex(0, playlistFadeTime, playlistLoop);
        }
        else
        {
            // 如果没有 playlist 但启用了按场景切换，则尝试根据当前激活场景播放音乐（首次进入）
            if (autoSwitchOnSceneLoad)
            {
                var active = SceneManager.GetActiveScene();
                PlayMusicForScene(active);
            }
            else if (defaultMusicClip != null && !playPlaylistOnAwake)
            {
                // 如果设置了默认音乐并且未通过 playlist 自动播放，则播放默认音乐
                PlayMusic(defaultMusicClip, playlistFadeTime, true);
            }
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
    /// 查找顺序：先按 Scene.name（非空精确匹配），再按 buildIndex（>=0 匹配），再尝试在 playlist 中按 name 匹配，最后使用 defaultMusicClip（如果有）。
    /// 如果找到匹配且与当前 musicSource.clip 相同，则不会重复切换。
    /// </summary>
    public void PlayMusicForScene(Scene scene)
    {
        if (scene.IsValid() == false) return;
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

        // 3. 如果没有显式映射，尝试在 playlist 中按 name 匹配
        if (match == null && playlist != null)
        {
            for (int i = 0; i < playlist.Length; i++)
            {
                if (playlist[i] != null && playlist[i].name == sceneName)
                {
                    PlayByIndex(i, playlistFadeTime, true);
                    return;
                }
            }
        }

        // 4. 如果找到了 Scene 映射，使用映射播放
        if (match != null && match.clip != null)
        {
            // 避免对相同 clip 做重复切换
            if (musicSource != null && musicSource.clip == match.clip && musicSource.isPlaying)
                return;

            float ft = match.fadeTime > 0f ? match.fadeTime : defaultSceneFadeTime;
            PlayMusic(match.clip, ft, match.loop);
            return;
        }

        // 5. 使用默认音乐（如果设置）
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
        // 创建一个临时 Scene-like 查询，优先使用 name 匹配逻辑
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

        // playlist 中按 name 查找
        if (playlist != null)
        {
            for (int i = 0; i < playlist.Length; i++)
            {
                if (playlist[i] != null && playlist[i].name == sceneName)
                {
                    PlayByIndex(i, playlistFadeTime, true);
                    return;
                }
            }
        }

        if (defaultMusicClip != null)
            PlayMusic(defaultMusicClip, defaultSceneFadeTime, true);
    }

    /// <summary>
    /// 确保 musicSource 和 sfxSource 存在；若未在 Inspector 指定则动态创建子对象和 AudioSource。
    /// </summary>
    private void EnsureAudioSources()
    {
        // 如果在 Inspector 中未指定 AudioSource，则自动创建
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

    // ========================
    // Playlist 控制方法
    // ========================

    /// <summary>
    /// 通过索引播放 playlist 中的曲目，index 越界会被忽略。
    /// </summary>
    public void PlayByIndex(int index, float? fadeTime = null, bool? loop = null)
    {
        if (playlist == null || playlist.Length == 0) return;
        if (index < 0 || index >= playlist.Length) return;
        var clip = playlist[index];
        if (clip == null) return;

        currentIndex = index;
        PlayMusic(clip, fadeTime ?? playlistFadeTime, loop ?? playlistLoop);
    }

    /// <summary>
    /// 通过名字播放，匹配第一个相同名字的 AudioClip（Inspector 中的 clip.name 或 Resources 名称）。
    /// </summary>
    public void PlayByName(string name, float? fadeTime = null, bool? loop = null)
    {
        if (string.IsNullOrEmpty(name) || playlist == null) return;
        for (int i = 0; i < playlist.Length; i++)
        {
            if (playlist[i] != null && playlist[i].name == name)
            {
                PlayByIndex(i, fadeTime, loop);
                return;
            }
        }
    }

    /// <summary>
    /// 随机播放列表中的一首。
    /// </summary>
    public void PlayRandom(float? fadeTime = null, bool? loop = null)
    {
        if (playlist == null || playlist.Length == 0) return;
        int idx = Random.Range(0, playlist.Length);
        PlayByIndex(idx, fadeTime, loop);
    }

    /// <summary>
    /// 播放下一首（到末尾循环回到开头，受 playlistLoop 控制）。
    /// </summary>
    public void Next(float? fadeTime = null, bool? loop = null)
    {
        if (playlist == null || playlist.Length == 0) return;
        int next;
        if (currentIndex < 0) next = 0;
        else next = currentIndex + 1;
        if (next >= playlist.Length)
        {
            if (loop ?? playlistLoop)
                next = 0;
            else
                return; // 不循环则不做任何事
        }
        PlayByIndex(next, fadeTime, loop);
    }

    /// <summary>
    /// 播放上一首（到开头循环到末尾，受 playlistLoop 控制）。
    /// </summary>
    public void Previous(float? fadeTime = null, bool? loop = null)
    {
        if (playlist == null || playlist.Length == 0) return;
        int prev;
        if (currentIndex < 0) prev = playlist.Length - 1;
        else prev = currentIndex - 1;
        if (prev < 0)
        {
            if (loop ?? playlistLoop)
                prev = playlist.Length - 1;
            else
                return;
        }
        PlayByIndex(prev, fadeTime, loop);
    }

    /// <summary>
    /// 停止 Playlist 的播放（使用指定或默认的淡出时间）。
    /// </summary>
    public void StopPlaylist(float? fadeTime = null)
    {
        StopMusic(fadeTime ?? playlistFadeTime);
        currentIndex = -1;
    }

    /// <summary>
    /// 返回当前播放索引（-1 表示未通过 Playlist 功能播放任何曲目）。
    /// </summary>
    public int GetCurrentPlaylistIndex() => currentIndex;

    /// <summary>
    /// 获取或设置整个 playlist（在运行时可以替换）。
    /// </summary>
    public AudioClip[] GetPlaylist() => playlist;
    public void SetPlaylist(AudioClip[] clips) => playlist = clips;

    // 在编辑器里如果改变了音量或 playlist 加载设置，立刻应用（方便调试）
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!isInitialized) return;
        ApplyVolumes();
        // 如果启用了 Resources 加载并且在编辑器中改变了路径，允许重新加载（谨慎）
        if (loadFromResources)
        {
            var clips = Resources.LoadAll<AudioClip>(resourcesPath);
            if (clips != null && clips.Length > 0)
            {
                playlist = clips;
            }
        }
    }
#endif
}