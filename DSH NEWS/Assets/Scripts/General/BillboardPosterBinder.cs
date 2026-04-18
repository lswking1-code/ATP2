using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BillboardPosterRule
{
    [Tooltip(">=0 时要求 ValueManage.day 与此相等；-1 表示不检查 day")]
    public int RequiredDay = -1;

    [Tooltip("非空时要求 GetSituation(SituationId) 为 true")]
    public string SituationId = "";

    public Material PosterMaterial;
}

[System.Serializable]
public class BillboardPosterTarget
{
    [Tooltip("若为空，则在子层级中按 ChildObjectName 查找")]
    public MeshRenderer TargetMeshRenderer;

    [Tooltip("TargetMeshRenderer 为空时，按此名称在子物体中递归查找")]
    public string ChildObjectName = "Ad01";

    [Tooltip("要替换的海报所在材质槽（通常为 0 或 1）")]
    public int MaterialSlotIndex;

    [Tooltip("自上而下第一条同时满足 day / situation 约束的规则生效；可最后放一条 RequiredDay=-1 且 SituationId 为空 作为默认")]
    public List<BillboardPosterRule> Rules = new List<BillboardPosterRule>();
}

/// <summary>
/// 根据 ValueManage 的 day 与 Situation 切换广告牌 MeshRenderer 上某一材质槽的海报材质。
/// </summary>
public class BillboardPosterBinder : MonoBehaviour
{
    [Tooltip("留空则在运行时 FindFirstObjectByType<ValueManage>()")]
    public ValueManage ValueManageOverride;

    [Tooltip("可选：与 ValueManager 上相同的资产，用于在数值变化时刷新海报")]
    public ValueEventSO ValueEvent;

    public List<BillboardPosterTarget> Targets = new List<BillboardPosterTarget>();

    private ValueManage _valueManage;

    private void Awake()
    {
        if (Targets == null || Targets.Count == 0)
        {
            Targets = new List<BillboardPosterTarget>
            {
                new BillboardPosterTarget { ChildObjectName = "Ad01", MaterialSlotIndex = 0 },
                new BillboardPosterTarget { ChildObjectName = "Ad02", MaterialSlotIndex = 0 }
            };
        }

        ResolveMeshRenderers();
    }

    private void Start()
    {
        RefreshPosters();
    }

    private void OnEnable()
    {
        if (ValueEvent != null)
            ValueEvent.OnEventRaised += OnValueEventRaised;
    }

    private void OnDisable()
    {
        if (ValueEvent != null)
            ValueEvent.OnEventRaised -= OnValueEventRaised;
    }

    private void OnValueEventRaised(int index, float value)
    {
        RefreshPosters();
    }

    private void ResolveMeshRenderers()
    {
        for (int i = 0; i < Targets.Count; i++)
        {
            BillboardPosterTarget t = Targets[i];
            if (t == null) continue;

            if (t.TargetMeshRenderer == null && !string.IsNullOrEmpty(t.ChildObjectName))
            {
                Transform found = FindChildTransformByName(transform, t.ChildObjectName);
                if (found != null)
                    t.TargetMeshRenderer = found.GetComponent<MeshRenderer>();
            }
        }
    }

    private static Transform FindChildTransformByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == name)
                return all[i];
        }
        return null;
    }

    /// <summary> 可在其它脚本在修改 Situation 后主动调用以立即刷新 </summary>
    public void RefreshPosters()
    {
        _valueManage = ValueManageOverride != null
            ? ValueManageOverride
            : FindFirstObjectByType<ValueManage>();

        if (_valueManage == null)
        {
            Debug.LogWarning("BillboardPosterBinder: 未找到 ValueManage，跳过海报刷新。", this);
            return;
        }

        for (int i = 0; i < Targets.Count; i++)
        {
            ApplyTarget(Targets[i], _valueManage);
        }
    }

    private static void ApplyTarget(BillboardPosterTarget target, ValueManage vm)
    {
        if (target == null || target.Rules == null || target.Rules.Count == 0)
            return;

        MeshRenderer mr = target.TargetMeshRenderer;
        if (mr == null)
            return;

        Material chosen = null;
        for (int r = 0; r < target.Rules.Count; r++)
        {
            BillboardPosterRule rule = target.Rules[r];
            if (rule == null) continue;

            if (!RuleMatches(vm, rule))
                continue;

            if (rule.PosterMaterial == null)
                continue;

            chosen = rule.PosterMaterial;
            break;
        }

        if (chosen == null)
            return;

        int slot = target.MaterialSlotIndex;
        Material[] mats = mr.materials;
        if (slot < 0 || slot >= mats.Length)
        {
            Debug.LogWarning($"BillboardPosterBinder: 材质槽 {slot} 无效（共 {mats.Length} 个槽）。", mr);
            return;
        }

        mats[slot] = chosen;
        mr.materials = mats;
    }

    private static bool RuleMatches(ValueManage vm, BillboardPosterRule rule)
    {
        if (rule.RequiredDay >= 0 && vm.day != rule.RequiredDay)
            return false;

        if (!string.IsNullOrEmpty(rule.SituationId) && !vm.GetSituation(rule.SituationId))
            return false;

        return true;
    }
}
