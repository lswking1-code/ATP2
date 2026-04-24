using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 由 ValueManage 条件触发的一次性场景断电流程：
/// 灯光闪烁 -> 熄灭 -> NPC 消失 -> 红色应急灯 -> 海报前单灯 -> 靠近切海报 -> 场景恢复。
/// </summary>
public class PowerOutageSceneSequence : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField, Tooltip("开启后必须先进入 TriggerZone 才会开始检测 ValueManage 条件。")]
    private bool requireTriggerZone = true;

    [SerializeField, Tooltip("进入触发区后是否只触发一次。")]
    private bool triggerZoneOneShot = true;

    [Header("Activation (ValueManage)")]
    [SerializeField, Tooltip(">=0 时要求 day 精确匹配；-1 表示不检查 day。")]
    private int targetDay = -1;

    [SerializeField, Tooltip("非空时要求 GetSituation(targetSituationId) 为 true（大小写敏感）。")]
    private string targetSituationId = "";

    [SerializeField, Tooltip("留空则自动查找场景中的 ValueManage。")]
    private ValueManage valueManage;

    [Header("Lights")]
    [SerializeField, Tooltip("若不填写，将自动收集场景全部 Light（包含未激活对象）。")]
    private List<Light> sceneLights = new List<Light>();

    [SerializeField, Tooltip("海报前唯一保持正常亮起的灯。")]
    private Light posterLamp;

    [SerializeField, Min(0.1f)]
    private float flickerDuration = 3f;

    [SerializeField, Min(0.01f)]
    private float flickerIntervalMin = 0.05f;

    [SerializeField, Min(0.01f)]
    private float flickerIntervalMax = 0.15f;

    [SerializeField, Min(0f), Tooltip("断电后到红灯阶段的等待时间。")]
    private float delayBeforeEmergencyRed = 2f;

    [SerializeField, Min(0f), Tooltip("红灯阶段持续多久后进入海报单灯阶段。")]
    private float emergencyRedDuration = 5f;

    [SerializeField, Tooltip("红灯颜色。")]
    private Color emergencyColor = Color.red;

    [SerializeField, Range(0.1f, 3f), Tooltip("红灯阶段亮度倍率。")]
    private float emergencyIntensityMultiplier = 0.9f;

    [Header("NPC")]
    [SerializeField, Tooltip("若为空，将自动按标签收集 NPC。")]
    private List<GameObject> npcObjects = new List<GameObject>();

    [SerializeField, Tooltip("自动收集 NPC 时使用的标签。")]
    private string npcTag = "NPC";

    [Header("Poster")]
    [SerializeField, Tooltip("用于检测玩家是否靠近海报的位置点。")]
    private Transform posterPoint;

    [SerializeField, Min(0.1f), Tooltip("玩家距离海报点小于等于该值时判定为靠近。")]
    private float posterApproachDistance = 2f;

    [SerializeField, Tooltip("玩家；留空则自动按 Player 标签查找。")]
    private Transform player;

    [SerializeField, Tooltip("要切换的海报 Renderer。")]
    private Renderer posterRenderer;

    [SerializeField, Min(0), Tooltip("海报所在材质槽位。")]
    private int posterMaterialIndex = 0;

    [SerializeField, Tooltip("靠近海报后切换到的材质。")]
    private Material switchedPosterMaterial;

    [SerializeField, Min(0f), Tooltip("海报切换后延迟多久恢复全场。")]
    private float restoreDelayAfterPosterSwitch = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField, Tooltip("灯光闪烁阶段音效。")] private AudioClip lightFlickerClip;
    [SerializeField, Tooltip("断电音效。")] private AudioClip powerCutClip;
    [SerializeField, Tooltip("应急灯/警报循环音效。")] private AudioClip emergencyLoopClip;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    [Header("Debug Runtime")]
    [SerializeField] private bool activatedByCondition;
    [SerializeField] private bool triggerZoneActivated;
    [SerializeField] private bool sequenceRunning;
    [SerializeField] private bool sequenceCompleted;

    private struct LightState
    {
        public bool Enabled;
        public Color Color;
        public float Intensity;
    }

    private readonly Dictionary<Light, LightState> _lightStates = new Dictionary<Light, LightState>();
    private readonly Dictionary<GameObject, bool> _npcStates = new Dictionary<GameObject, bool>();

    private void Awake()
    {
        if (valueManage == null) valueManage = FindFirstObjectByType<ValueManage>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        TryAssignPlayer();
        ResolveLightsIfNeeded();
        ResolveNpcsIfNeeded();
    }

    private void Update()
    {
        if (sequenceCompleted || sequenceRunning) return;
        if (requireTriggerZone && !triggerZoneActivated) return;

        activatedByCondition = IsActivatedByValueManage();
        if (!activatedByCondition) return;

        StartCoroutine(RunSequence());
    }

    /// <summary>
    /// 由外部 TriggerZone 调用。仅当进入者是 Player 时激活。
    /// </summary>
    public void NotifyTriggerEnter(Collider other)
    {
        if (other == null || !other.CompareTag("Player")) return;
        if (sequenceCompleted) return;
        if (triggerZoneOneShot && triggerZoneActivated) return;
        triggerZoneActivated = true;
    }

    private bool IsActivatedByValueManage()
    {
        if (valueManage == null) valueManage = FindFirstObjectByType<ValueManage>();
        if (valueManage == null) return false;

        bool dayOk = targetDay < 0 || valueManage.day == targetDay;
        bool situationOk = string.IsNullOrWhiteSpace(targetSituationId) || valueManage.GetSituation(targetSituationId);
        return dayOk && situationOk;
    }

    private IEnumerator RunSequence()
    {
        sequenceRunning = true;
        CacheCurrentStates();

        yield return StartCoroutine(FlickerLightsRoutine(flickerDuration));
        SetAllLightsOff();
        PlayOneShot(powerCutClip);
        SetAllNpcVisible(false);

        if (delayBeforeEmergencyRed > 0f)
            yield return new WaitForSeconds(delayBeforeEmergencyRed);

        EnableEmergencyRedLights();
        StartEmergencyLoop();

        if (emergencyRedDuration > 0f)
            yield return new WaitForSeconds(emergencyRedDuration);

        EnablePosterLampOnly();

        yield return StartCoroutine(WaitPlayerApproachPoster());
        SwitchPosterMaterial();

        if (restoreDelayAfterPosterSwitch > 0f)
            yield return new WaitForSeconds(restoreDelayAfterPosterSwitch);

        RestoreScene();
        sequenceRunning = false;
        sequenceCompleted = true;
    }

    private void ResolveLightsIfNeeded()
    {
        if (sceneLights != null && sceneLights.Count > 0) return;

        Light[] allLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sceneLights = new List<Light>(allLights);
    }

    private void ResolveNpcsIfNeeded()
    {
        if (npcObjects != null && npcObjects.Count > 0) return;
        if (string.IsNullOrWhiteSpace(npcTag)) return;

        GameObject[] found = GameObject.FindGameObjectsWithTag(npcTag);
        npcObjects = new List<GameObject>(found);
    }

    private void CacheCurrentStates()
    {
        _lightStates.Clear();
        for (int i = 0; i < sceneLights.Count; i++)
        {
            Light l = sceneLights[i];
            if (l == null) continue;
            _lightStates[l] = new LightState
            {
                Enabled = l.enabled,
                Color = l.color,
                Intensity = l.intensity
            };
        }

        _npcStates.Clear();
        for (int i = 0; i < npcObjects.Count; i++)
        {
            GameObject npc = npcObjects[i];
            if (npc == null) continue;
            _npcStates[npc] = npc.activeSelf;
        }

    }

    private IEnumerator FlickerLightsRoutine(float duration)
    {
        PlayOneShot(lightFlickerClip);

        if (duration <= 0f) yield break;

        float timer = 0f;
        while (timer < duration)
        {
            for (int i = 0; i < sceneLights.Count; i++)
            {
                Light l = sceneLights[i];
                if (l == null) continue;
                l.enabled = !l.enabled;
            }

            float wait = Random.Range(flickerIntervalMin, flickerIntervalMax);
            wait = Mathf.Max(0.01f, wait);
            timer += wait;
            yield return new WaitForSeconds(wait);
        }
    }

    private void SetAllLightsOff()
    {
        for (int i = 0; i < sceneLights.Count; i++)
        {
            Light l = sceneLights[i];
            if (l != null) l.enabled = false;
        }
    }

    private void EnableEmergencyRedLights()
    {
        for (int i = 0; i < sceneLights.Count; i++)
        {
            Light l = sceneLights[i];
            if (l == null) continue;

            l.enabled = true;
            if (_lightStates.TryGetValue(l, out LightState state))
            {
                l.intensity = Mathf.Max(0.01f, state.Intensity * emergencyIntensityMultiplier);
            }
            l.color = emergencyColor;
        }
    }

    private void EnablePosterLampOnly()
    {
        SetAllLightsOff();
        if (posterLamp == null) return;

        posterLamp.enabled = true;
        if (_lightStates.TryGetValue(posterLamp, out LightState original))
        {
            posterLamp.color = original.Color;
            posterLamp.intensity = original.Intensity;
        }
    }

    private IEnumerator WaitPlayerApproachPoster()
    {
        while (true)
        {
            if (player == null) TryAssignPlayer();
            if (player == null || posterPoint == null)
            {
                yield return null;
                continue;
            }

            float sqr = (player.position - posterPoint.position).sqrMagnitude;
            if (sqr <= posterApproachDistance * posterApproachDistance)
                yield break;

            yield return null;
        }
    }

    private void SwitchPosterMaterial()
    {
        if (posterRenderer == null || switchedPosterMaterial == null) return;

        Material[] mats = posterRenderer.materials;
        if (posterMaterialIndex < 0 || posterMaterialIndex >= mats.Length) return;

        mats[posterMaterialIndex] = switchedPosterMaterial;
        posterRenderer.materials = mats;
    }

    private void RestoreScene()
    {
        StopEmergencyLoop();

        foreach (KeyValuePair<Light, LightState> pair in _lightStates)
        {
            if (pair.Key == null) continue;

            pair.Key.enabled = pair.Value.Enabled;
            pair.Key.color = pair.Value.Color;
            pair.Key.intensity = pair.Value.Intensity;
        }

        foreach (KeyValuePair<GameObject, bool> pair in _npcStates)
        {
            if (pair.Key == null) continue;
            pair.Key.SetActive(pair.Value);
        }
    }

    private void SetAllNpcVisible(bool visible)
    {
        for (int i = 0; i < npcObjects.Count; i++)
        {
            GameObject npc = npcObjects[i];
            if (npc == null) continue;
            npc.SetActive(visible);
        }
    }

    private void TryAssignPlayer()
    {
        if (player != null) return;
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) player = go.transform;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.loop = false;
        audioSource.PlayOneShot(clip, sfxVolume);
    }

    private void StartEmergencyLoop()
    {
        if (audioSource == null || emergencyLoopClip == null) return;

        audioSource.Stop();
        audioSource.clip = emergencyLoopClip;
        audioSource.loop = true;
        audioSource.volume = sfxVolume;
        audioSource.Play();
    }

    private void StopEmergencyLoop()
    {
        if (audioSource == null) return;
        if (audioSource.isPlaying) audioSource.Stop();
        audioSource.loop = false;
    }
}
