using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Collider))]
public class PosterDizzyHallucinationTrigger : MonoBehaviour
{
    [Header("Activation (ValueManage)")]
    [SerializeField, Tooltip(">=0 时要求 day 精确匹配；-1 表示不检查 day。")]
    private int targetDay = -1;

    [SerializeField, Tooltip("非空时要求 GetSituation(targetSituationId) 为 true（大小写敏感）。")]
    private string targetSituationId = "";

    [SerializeField, Tooltip("留空则自动查找场景中的 ValueManage。")]
    private ValueManage valueManage;

    [Header("References")]
    [SerializeField, Tooltip("用于控制眩晕参数的 GlitchControl。")]
    private GlitchControl glitchControl;

    [SerializeField, Tooltip("循环幻听音效。")]
    private AudioClip hallucinationLoopClip;

    [SerializeField, Tooltip("可选：幻听音效 3D 声源点。为空时使用当前物体。")]
    private Transform audioAnchor;

    [Header("Poster Replace On Activation")]
    [SerializeField, Tooltip("要替换材质的海报 Renderer。")]
    private Renderer posterRenderer;

    [SerializeField, Tooltip("激活后替换到的海报材质。")]
    private Material activatedPosterMaterial;

    [SerializeField, Tooltip("NPC/玩家朝向点；为空时使用当前触发器位置。")]
    private Transform posterLookTarget;

    [Header("Dizzy Target Values")]
    [SerializeField, Range(0f, 1f)] private float targetScanLineJitter = 0.65f;
    [SerializeField, Range(0f, 1f)] private float targetVerticalJump = 0.55f;
    [SerializeField, Range(0f, 1f)] private float targetHorizontalShake = 0.45f;
    [SerializeField, Range(0f, 1f)] private float targetColorDrift = 0.4f;

    [Header("Transition")]
    [SerializeField, Min(0f), Tooltip("进入眩晕状态过渡时长（秒）。")]
    private float enterDuration = 0.35f;

    [SerializeField, Min(0f), Tooltip("退出眩晕状态过渡时长（秒）。")]
    private float exitDuration = 0.45f;

    [Header("Audio 3D")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
    [SerializeField, Min(0f)] private float minDistance = 1.5f;
    [SerializeField, Min(0f)] private float maxDistance = 20f;
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    [SerializeField, Min(0f)] private float audioFadeOut = 0.2f;

    [Header("Runtime (Read Only)")]
    [SerializeField] private bool playerInside;
    [SerializeField] private bool isActivatedByCondition;
    [SerializeField] private bool posterReplaced;

    private Coroutine transitionCoroutine;
    private AudioSource loopSource;
    private GlitchSnapshot enterSnapshot;
    private bool hasSnapshot;
    private readonly Dictionary<NavMeshAgent, bool> npcStoppedState = new Dictionary<NavMeshAgent, bool>();
    private Material originalPosterMaterial;
    private float nextConditionCheckTime;

    private struct GlitchSnapshot
    {
        public float scanLineJitter;
        public float verticalJump;
        public float horizontalShake;
        public float colorDrift;
    }

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (posterRenderer == null)
        {
            posterRenderer = GetComponentInChildren<Renderer>();
        }
    }

    private void Update()
    {
        TryReplacePosterByCondition();
    }

    private void OnTriggerEnter(Collider other)
    {
        bool isPlayer = other.CompareTag("Player");
        bool isNpc = other.CompareTag("NPC");
        if (!isPlayer && !isNpc) return;

        isActivatedByCondition = IsActivatedByValueManage();
        if (!isActivatedByCondition)
        {
            return;
        }

        if (isPlayer)
        {
            playerInside = true;
            BeginEnter();
            return;
        }

        if (isNpc)
        {
            HandleNpcEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (!playerInside) return;
            playerInside = false;
            BeginExit();
            return;
        }

        if (other.CompareTag("NPC"))
        {
            HandleNpcExit(other);
        }
    }

    private void OnDisable()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        StopLoopAudio();
        RestoreSnapshotImmediate();
        RestoreAllNpcStates();
        playerInside = false;
    }

    private void BeginEnter()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        transitionCoroutine = StartCoroutine(EnterRoutine());
    }

    private void BeginExit()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        transitionCoroutine = StartCoroutine(ExitRoutine());
    }

    private IEnumerator EnterRoutine()
    {
        CaptureSnapshotIfNeeded();
        StartLoopAudio();
        yield return LerpTo(
            targetScanLineJitter,
            targetVerticalJump,
            targetHorizontalShake,
            targetColorDrift,
            enterDuration);
        transitionCoroutine = null;
    }

    private IEnumerator ExitRoutine()
    {
        StopLoopAudio();
        if (hasSnapshot)
        {
            yield return LerpTo(
                enterSnapshot.scanLineJitter,
                enterSnapshot.verticalJump,
                enterSnapshot.horizontalShake,
                enterSnapshot.colorDrift,
                exitDuration);
            hasSnapshot = false;
        }

        transitionCoroutine = null;
    }

    private void CaptureSnapshotIfNeeded()
    {
        if (glitchControl == null || hasSnapshot)
        {
            return;
        }

        enterSnapshot = new GlitchSnapshot
        {
            scanLineJitter = glitchControl.scanLineJitter,
            verticalJump = glitchControl.verticalJump,
            horizontalShake = glitchControl.horizontalShake,
            colorDrift = glitchControl.colorDrift
        };
        hasSnapshot = true;
    }

    private IEnumerator LerpTo(float scanLineJitter, float verticalJump, float horizontalShake, float colorDrift, float duration)
    {
        if (glitchControl == null)
        {
            yield break;
        }

        float fromScanLine = glitchControl.scanLineJitter;
        float fromVertical = glitchControl.verticalJump;
        float fromHorizontal = glitchControl.horizontalShake;
        float fromColor = glitchControl.colorDrift;

        if (duration <= 0f)
        {
            glitchControl.scanLineJitter = scanLineJitter;
            glitchControl.verticalJump = verticalJump;
            glitchControl.horizontalShake = horizontalShake;
            glitchControl.colorDrift = colorDrift;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            glitchControl.scanLineJitter = Mathf.Lerp(fromScanLine, scanLineJitter, t);
            glitchControl.verticalJump = Mathf.Lerp(fromVertical, verticalJump, t);
            glitchControl.horizontalShake = Mathf.Lerp(fromHorizontal, horizontalShake, t);
            glitchControl.colorDrift = Mathf.Lerp(fromColor, colorDrift, t);
            yield return null;
        }

        glitchControl.scanLineJitter = scanLineJitter;
        glitchControl.verticalJump = verticalJump;
        glitchControl.horizontalShake = horizontalShake;
        glitchControl.colorDrift = colorDrift;
    }

    private void StartLoopAudio()
    {
        if (loopSource != null || hallucinationLoopClip == null)
        {
            return;
        }

        AudioManager manager = AudioManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[PosterDizzyHallucinationTrigger] AudioManager.Instance not found.", this);
            return;
        }

        Transform anchor = audioAnchor != null ? audioAnchor : transform;
        loopSource = manager.PlayLoopSFX3D(
            hallucinationLoopClip,
            anchor,
            volume,
            minDistance,
            maxDistance,
            rolloffMode);
    }

    private void StopLoopAudio()
    {
        if (loopSource == null)
        {
            return;
        }

        AudioManager manager = AudioManager.Instance;
        if (manager != null)
        {
            manager.StopLoopSFX(loopSource, audioFadeOut);
        }
        else
        {
            Destroy(loopSource.gameObject);
        }

        loopSource = null;
    }

    private void RestoreSnapshotImmediate()
    {
        if (!hasSnapshot || glitchControl == null)
        {
            hasSnapshot = false;
            return;
        }

        glitchControl.scanLineJitter = enterSnapshot.scanLineJitter;
        glitchControl.verticalJump = enterSnapshot.verticalJump;
        glitchControl.horizontalShake = enterSnapshot.horizontalShake;
        glitchControl.colorDrift = enterSnapshot.colorDrift;
        hasSnapshot = false;
    }

    private bool IsActivatedByValueManage()
    {
        if (valueManage == null) valueManage = FindFirstObjectByType<ValueManage>();
        if (valueManage == null) return false;

        bool dayOk = targetDay < 0 || valueManage.day == targetDay;
        bool situationOk = string.IsNullOrWhiteSpace(targetSituationId) || valueManage.GetSituation(targetSituationId);
        return dayOk && situationOk;
    }

    private void ReplacePosterOnActivation()
    {
        if (posterReplaced) return;

        if (posterRenderer == null)
        {
            return;
        }

        if (activatedPosterMaterial == null)
        {
            return;
        }

        if (originalPosterMaterial == null)
        {
            originalPosterMaterial = posterRenderer.sharedMaterial;
        }

        posterRenderer.material = activatedPosterMaterial;
        posterReplaced = true;
    }

    private void TryReplacePosterByCondition()
    {
        if (posterReplaced) return;
        if (Time.unscaledTime < nextConditionCheckTime) return;
        nextConditionCheckTime = Time.unscaledTime + 0.25f;

        isActivatedByCondition = IsActivatedByValueManage();
        if (!isActivatedByCondition) return;

        ReplacePosterOnActivation();
    }

    private void HandleNpcEnter(Collider other)
    {
        NavMeshAgent agent = other.GetComponentInParent<NavMeshAgent>();
        if (agent != null)
        {
            if (!npcStoppedState.ContainsKey(agent))
            {
                npcStoppedState.Add(agent, agent.isStopped);
            }

            agent.isStopped = true;
            agent.ResetPath();
        }

        Transform npcTransform = agent != null ? agent.transform : other.transform;
        RotateTransformTowardsPoster(npcTransform);
    }

    private void HandleNpcExit(Collider other)
    {
        NavMeshAgent agent = other.GetComponentInParent<NavMeshAgent>();
        if (agent == null) return;

        if (npcStoppedState.TryGetValue(agent, out bool wasStopped))
        {
            agent.isStopped = wasStopped;
            npcStoppedState.Remove(agent);
        }
        else
        {
            agent.isStopped = false;
        }
    }

    private void RestoreAllNpcStates()
    {
        foreach (KeyValuePair<NavMeshAgent, bool> pair in npcStoppedState)
        {
            if (pair.Key == null) continue;
            pair.Key.isStopped = pair.Value;
        }

        npcStoppedState.Clear();
    }

    private void RotateTransformTowardsPoster(Transform target)
    {
        if (target == null) return;

        Vector3 lookPos = posterLookTarget != null ? posterLookTarget.position : transform.position;
        Vector3 dir = lookPos - target.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        target.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }
}
