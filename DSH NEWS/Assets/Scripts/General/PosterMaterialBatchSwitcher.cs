using System.Collections.Generic;
using UnityEngine;

public class PosterMaterialBatchSwitcher : MonoBehaviour
{
    [Header("Condition (ValueManage)")]
    [SerializeField, Tooltip("留空时自动查找场景中的 ValueManage。")]
    private ValueManage valueManage;

    [SerializeField, Tooltip("要求 ValueManage.day 精确等于该值。")]
    private int requiredDay = 0;

    [SerializeField, Tooltip("要求 ValueManage.GetSituation(id) 为 true。")]
    private string requiredSituationId = "";

    [SerializeField, Tooltip("可选：若赋值则在 ValueEvent 变更时立即复查条件。")]
    private ValueEventSO valueEvent;

    [Header("Poster Targets")]
    [SerializeField, Tooltip("需要替换海报材质的 Renderer 列表（手动拖拽）。")]
    private List<Renderer> posterRenderers = new List<Renderer>();

    [SerializeField, Tooltip("替换到的目标材质。")]
    private Material targetMaterial;

    [SerializeField, Tooltip("要替换的材质槽下标，默认 0。")]
    private int materialSlotIndex = 0;

    [Header("Check")]
    [SerializeField, Min(0.05f), Tooltip("轮询检查间隔（秒）。")]
    private float checkInterval = 0.25f;

    [Header("Runtime (Read Only)")]
    [SerializeField] private bool hasSwitched;

    private float nextCheckTime;

    private void Start()
    {
        TrySwitchPostersByCondition();
    }

    private void OnEnable()
    {
        if (valueEvent != null)
        {
            valueEvent.OnEventRaised += OnValueEventRaised;
        }
    }

    private void OnDisable()
    {
        if (valueEvent != null)
        {
            valueEvent.OnEventRaised -= OnValueEventRaised;
        }
    }

    private void Update()
    {
        if (hasSwitched)
        {
            return;
        }

        if (Time.unscaledTime < nextCheckTime)
        {
            return;
        }

        nextCheckTime = Time.unscaledTime + checkInterval;
        TrySwitchPostersByCondition();
    }

    private void OnValueEventRaised(int index, float value)
    {
        TrySwitchPostersByCondition();
    }

    private void TrySwitchPostersByCondition()
    {
        if (hasSwitched)
        {
            return;
        }

        if (!IsConditionMet())
        {
            return;
        }

        if (targetMaterial == null)
        {
            Debug.LogWarning("[PosterMaterialBatchSwitcher] targetMaterial 为空，无法执行替换。", this);
            return;
        }

        bool replacedAny = false;
        for (int i = 0; i < posterRenderers.Count; i++)
        {
            Renderer poster = posterRenderers[i];
            if (!TryApplyMaterialToRenderer(poster))
            {
                continue;
            }

            replacedAny = true;
        }

        if (!replacedAny)
        {
            Debug.LogWarning("[PosterMaterialBatchSwitcher] 未找到可替换的海报 Renderer。", this);
            return;
        }

        hasSwitched = true;
    }

    private bool IsConditionMet()
    {
        if (valueManage == null)
        {
            valueManage = FindFirstObjectByType<ValueManage>();
        }

        if (valueManage == null)
        {
            Debug.LogWarning("[PosterMaterialBatchSwitcher] 未找到 ValueManage，无法检测条件。", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(requiredSituationId))
        {
            Debug.LogWarning("[PosterMaterialBatchSwitcher] requiredSituationId 为空，条件不会成立。", this);
            return false;
        }

        bool dayMatched = valueManage.day == requiredDay;
        bool situationMatched = valueManage.GetSituation(requiredSituationId);
        return dayMatched && situationMatched;
    }

    private bool TryApplyMaterialToRenderer(Renderer poster)
    {
        if (poster == null)
        {
            Debug.LogWarning("[PosterMaterialBatchSwitcher] Renderer 列表中存在空引用，已跳过。", this);
            return false;
        }

        Material[] materials = poster.materials;
        if (materials == null || materials.Length == 0)
        {
            Debug.LogWarning($"[PosterMaterialBatchSwitcher] 对象 {poster.name} 没有材质槽，已跳过。", poster);
            return false;
        }

        if (materialSlotIndex < 0 || materialSlotIndex >= materials.Length)
        {
            Debug.LogWarning($"[PosterMaterialBatchSwitcher] 对象 {poster.name} 材质槽越界：slot={materialSlotIndex}，count={materials.Length}。", poster);
            return false;
        }

        materials[materialSlotIndex] = targetMaterial;
        poster.materials = materials;
        return true;
    }
}
