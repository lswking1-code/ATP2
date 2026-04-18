using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ȫ����Ƶ����������������
/// ���𱳾����ֺ�һ������Ч�Ĳ��š�����/�����Լ��������ơ�
/// ֧�ְ������Զ��л��������֣��� Inspector ��Ϊ��ͬ����ָ����Ӧ�� AudioClip���� Scene Name �� Build Index�������س���ʱ�Զ��л���֧�ֵ���/��������
/// ���齫��������ڳ����е�һ�������ϣ���������ʱ������������������Ϊ DontDestroyOnLoad ���ڳ����临�á�
/// </summary>
public class AudioManager : MonoBehaviour
{
    // ����ʵ��
    public static AudioManager Instance { get; private set; }

    [Header("Sources")]
    [SerializeField, Tooltip("���ڲ��ű������֣�֧�ֵ���/������ѭ��")]
    private AudioSource musicSource;

    [SerializeField, Tooltip("���ڲ���һ������Ч��ʹ�� PlayOneShot��")]
    private AudioSource sfxSource;

    [Header("Volumes")]
    [SerializeField, Range(0f, 1f), Tooltip("����������������")]
    private float musicVolume = 1f;

    [SerializeField, Range(0f, 1f), Tooltip("��Ч��������")]
    private float sfxVolume = 1f;

    // Scene-based music mapping
    [Header("Scene Music")]
    [SerializeField, Tooltip("����ʱ���ڳ�������ʱ����ӳ���Զ��л���������")]
    private bool autoSwitchOnSceneLoad = true;

    [SerializeField, Tooltip("������û��ƥ����Ŀʱʹ�õĺ󱸱������֣������գ�")]
    private AudioClip defaultMusicClip;

    [SerializeField, Tooltip("Ĭ�ϵ���/����ʱ�����룩����������Ŀδָ��ʱʹ��")]
    private float defaultSceneFadeTime = 0.5f;

    [System.Serializable]
    private class SceneMusicEntry
    {
        [Tooltip("����ͨ�� Scene Name ƥ�䣨Exact match����������գ�����ʹ�� Build Index ƥ�䡣")]
        public string sceneName = "";
        [Tooltip("��ѡ��ͨ�� Build Index ƥ�䳡����-1 ��ʾ��ʹ�á�")]
        public int sceneBuildIndex = -1;
        [Tooltip("Ϊ�ó������ŵ� AudioClip")]
        public AudioClip clip;
        [Tooltip("��� <= 0 ��ʹ��Ĭ�ϵĵ���/����ʱ��")]
        public float fadeTime = -1f;
        [Tooltip("����ʱ�Ƿ�ѭ��")]
        public bool loop = true;
    }

    [SerializeField, Tooltip("������ӳ��������б���֧�ְ� Scene Name �� Build Index ƥ��")]
    private List<SceneMusicEntry> sceneMusic = new List<SceneMusicEntry>();

    // ��ǰ���ڵ���/�����������ֵ�Э������
    private Coroutine musicFadeCoroutine;
    // ����Ƿ���ɳ�ʼ�������ڱ༭�� OnValidate ��飩
    private bool isInitialized = false;

    /// <summary>
    /// Awake����ʼ��������ȷ����Դ���ڲ�Ӧ��Ĭ��������
    /// �����������ʵ�������ٵ�ǰ����
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

        // ���ĳ��������¼��������Զ��л��������֣�
        if (autoSwitchOnSceneLoad)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // �״ν���ʱ���ݵ�ǰ��������
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
        // ȡ�����ģ������ڴ�й©
        if (autoSwitchOnSceneLoad)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // ������������
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// �������ػص��������³����л��������֣�������ã���
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!autoSwitchOnSceneLoad) return;
        PlayMusicForScene(scene);
    }

    /// <summary>
    /// ���� Scene �������ӳ�䲢���Ŷ�Ӧ�ı������֡�
    /// ����˳���Ȱ� Scene.name���ǿվ�ȷƥ�䣩���ٰ� buildIndex��>=0 ƥ�䣩�����ʹ�� defaultMusicClip������У���
    /// ����ҵ�ƥ�����뵱ǰ musicSource.clip ��ͬ���򲻻��ظ��л���
    /// </summary>
    public void PlayMusicForScene(Scene scene)
    {
        if (!scene.IsValid()) return;
        string sceneName = scene.name;
        int buildIndex = scene.buildIndex;

        SceneMusicEntry match = null;

        // 1. �� Name ��ȷƥ��
        for (int i = 0; i < sceneMusic.Count; i++)
        {
            var e = sceneMusic[i];
            if (!string.IsNullOrEmpty(e.sceneName) && e.sceneName == sceneName)
            {
                match = e;
                break;
            }
        }

        // 2. �� Build Index ƥ�䣨ֻ����û��ͨ�� name ƥ��ʱ��
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

        // ����ҵ��� Scene ӳ�䣬ʹ��ӳ�䲥��
        if (match != null && match.clip != null)
        {
            // �������ͬ clip ���ظ��л�
            if (musicSource != null && musicSource.clip == match.clip && musicSource.isPlaying)
                return;

            float ft = match.fadeTime > 0f ? match.fadeTime : defaultSceneFadeTime;
            PlayMusic(match.clip, ft, match.loop);
            return;
        }

        // ʹ��Ĭ�����֣�������ã�
        if (defaultMusicClip != null)
        {
            if (musicSource != null && musicSource.clip == defaultMusicClip && musicSource.isPlaying)
                return;

            PlayMusic(defaultMusicClip, defaultSceneFadeTime, true);
        }
        else
        {
            // ���û���κ����֣�ֹͣ��ǰ���ţ�������
            StopMusic(defaultSceneFadeTime);
        }
    }

    /// <summary>
    /// ͨ��������������ű����ã��������š�
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
    /// ȷ�� musicSource �� sfxSource ���ڣ���δ�� Inspector ָ����̬�����Ӷ���� AudioSource��
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
    /// �����л�������ֵӦ�õ���Ӧ�� AudioSource��
    /// ������ʱ��༭���޸�ֵ���������Ч��
    /// </summary>
    private void ApplyVolumes()
    {
        if (musicSource != null)
            musicSource.volume = musicVolume;
        if (sfxSource != null)
            sfxSource.volume = sfxVolume;
    }

    /// <summary>
    /// ����һ������Ч��PlayOneShot�����ῼ�� sfxVolume ������
    /// clip Ϊ null �� sfxSource ������ʱ��ִ�С�
    /// </summary>
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume * sfxVolume));
    }

    /// <summary>
    /// ��ָ���������겥�� 3D һ������Ч��
    /// ʹ����ʱ AudioSource��������Ϻ��Զ����١�
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

        // ���� pitch �仯�󣬰�ʵ�ʲ���ʱ�����٣����ⳡ��������
        float pitchAbs = Mathf.Max(0.01f, Mathf.Abs(source.pitch));
        float lifeTime = (clip.length / pitchAbs) + 0.1f;
        Destroy(tempGo, lifeTime);

        return source;
    }

    /// <summary>
    /// ��ָ���ҵ㣨Transform��λ�ò��� 3D һ������Ч��
    /// ��Դ�����ҵ��ƶ���������Ϻ��Զ����١�
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
    /// ��ָ���ҵ㲥�ſ�ֹͣ�� 3D ѭ����Ч��
    /// ���� StopLoopSFX(source) ��ֹͣ��������
    /// </summary>
    public AudioSource PlayLoopSFX3D(
        AudioClip clip,
        Transform anchor,
        float volume = 1f,
        float minDistance = 1f,
        float maxDistance = 25f,
        AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic)
    {
        if (clip == null) return null;

        var loopGo = new GameObject($"Loop3D_{clip.name}");
        if (anchor != null)
        {
            loopGo.transform.SetParent(anchor, false);
            loopGo.transform.localPosition = Vector3.zero;
        }

        var source = loopGo.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = rolloffMode;
        source.minDistance = Mathf.Max(0f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, maxDistance);
        source.loop = true;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume * sfxVolume);
        source.Play();

        return source;
    }

    /// <summary>
    /// ֹͣ�� PlayLoopSFX3D ������ѭ����Ч����������ʱ����
    /// fadeOut <= 0 ʱ����ֹͣ������ʱ��������ֹͣ��
    /// </summary>
    public void StopLoopSFX(AudioSource source, float fadeOut = 0f)
    {
        if (source == null) return;

        if (fadeOut <= 0f)
        {
            source.Stop();
            Destroy(source.gameObject);
            return;
        }

        StartCoroutine(FadeOutAndDestroySource(source, fadeOut));
    }

    /// <summary>
    /// �������ò����ű������֣��޵��뵭������
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
    /// ʹ�õ�����ǰ���֡����������ֵķ�ʽ�л��������֡�
    /// fadeTime Ϊ����/����ʱ�����룩��
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
    /// ֹͣ��ǰ�������֣��ɴ���������
    /// </summary>
    public void StopMusic(float fadeTime = 0.5f)
    {
        if (musicSource == null) return;
        musicFadeCoroutine = StopCoroutineIfRunning(musicFadeCoroutine);
        musicFadeCoroutine = StartCoroutine(StopMusicRoutine(fadeTime));
    }

    /// <summary>
    /// ���ñ�����������������0-1����������Ӧ�õ� musicSource��
    /// </summary>
    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        if (musicSource != null)
            musicSource.volume = musicVolume;
    }

    /// <summary>
    /// ������Ч����������0-1����������Ӧ�õ� sfxSource��
    /// </summary>
    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        if (sfxSource != null)
            sfxSource.volume = sfxVolume;
    }

    /// <summary>
    /// ����/ȡ����������Ӱ��˹��������е���Դ����
    /// </summary>
    public void SetMute(bool mute)
    {
        if (musicSource != null) musicSource.mute = mute;
        if (sfxSource != null) sfxSource.mute = mute;
    }

    /// <summary>
    /// ����ǰ���ֵ������л��� newClip ��Э��ʵ�֡�
    /// �� newClip Ϊ null ���������ֹͣ��
    /// </summary>
    private IEnumerator FadeMusicRoutine(AudioClip newClip, float duration)
    {
        float startVol = musicSource.volume;
        float target = 0f;

        // ������ǰ����
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float factor = Mathf.Clamp01(t / duration);
            // ���� musicVolume ��֧��ȫ����������
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

        // ���뵽�趨�� musicVolume���� 0 �� 1 �Ĳ�ֵ���ٳ��� musicVolume��
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
    /// ����ǰ���ֵ�����ֹͣ��Э��ʵ�֡�
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
    /// ��������Э������������ֹͣ�������� null�����㸳ֵ/��ʽ���ã���
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

    private IEnumerator FadeOutAndDestroySource(AudioSource source, float duration)
    {
        if (source == null) yield break;

        float startVolume = source.volume;
        float t = 0f;
        while (t < duration && source != null)
        {
            t += Time.unscaledDeltaTime;
            float factor = Mathf.Clamp01(t / duration);
            source.volume = Mathf.Lerp(startVolume, 0f, factor);
            yield return null;
        }

        if (source == null) yield break;
        source.Stop();
        Destroy(source.gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!isInitialized) return;
        ApplyVolumes();
    }
#endif
}