using System.Collections;
using UnityEngine;

/// <summary>
/// 全局音频管理器：负责背景音乐与音效播放、淡入淡出与音量控制（单例）。
/// 挂在场景任意 GameObject 上或在运行时自动创建一个持久化实例。
/// </summary>
public class AudioManager : MonoBehaviour
{
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

    private Coroutine musicFadeCoroutine;
    private bool isInitialized = false;

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

        isInitialized = true;
    }

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

    private void ApplyVolumes()
    {
        if (musicSource != null)
            musicSource.volume = musicVolume;
        if (sfxSource != null)
            sfxSource.volume = sfxVolume;
    }

    // 播放一次性音效（立即）
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume * sfxVolume));
    }

    // 直接设置并播放背景音乐（无淡入淡出）
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

    // 使用淡入淡出切换背景音乐
    public void PlayMusic(AudioClip clip, float fadeTime = 0.5f, bool loop = true)
    {
        if (musicSource == null)
            return;

        musicSource.loop = loop;
        musicFadeCoroutine = StopCoroutineIfRunning(musicFadeCoroutine);
        musicFadeCoroutine = StartCoroutine(FadeMusicRoutine(clip, fadeTime));
    }

    // 停止音乐（可淡出）
    public void StopMusic(float fadeTime = 0.5f)
    {
        if (musicSource == null) return;
        musicFadeCoroutine = StopCoroutineIfRunning(musicFadeCoroutine);
        musicFadeCoroutine = StartCoroutine(StopMusicRoutine(fadeTime));
    }

    // 设置背景音乐音量（0-1）
    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        if (musicSource != null)
            musicSource.volume = musicVolume;
    }

    // 设置音效音量（0-1）
    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        if (sfxSource != null)
            sfxSource.volume = sfxVolume;
    }

    // 静音/取消静音（仅影响此管理器的音源）
    public void SetMute(bool mute)
    {
        if (musicSource != null) musicSource.mute = mute;
        if (sfxSource != null) sfxSource.mute = mute;
    }

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
            musicSource.volume = Mathf.Lerp(startVol, target, factor) * musicVolume;
            yield return null;
        }

        musicSource.Stop();
        musicSource.clip = newClip;

        if (newClip == null)
            yield break;

        musicSource.Play();

        // 淡入到设定的 musicVolume
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
    // 淡出并停止当前音乐
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
    // 停止协程并返回 null（方便赋值）
    private Coroutine StopCoroutineIfRunning(Coroutine c)
    {
        if (c != null)
        {
            StopCoroutine(c);
            return null;
        }
        return null;
    }

    // 在编辑器里如果改变了音量，立刻应用（方便调试）
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!isInitialized) return;
        ApplyVolumes();
    }
#endif
}