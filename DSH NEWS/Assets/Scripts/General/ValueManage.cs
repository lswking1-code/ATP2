using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SituationFlag
{
    [Tooltip("情况标识，如 A、B、C，供其他脚本按名称查询")]
    public string Id = "";
    [Tooltip("该情况是否已发生")]
    public bool Occurred = false;
}

public class ValueManage : MonoBehaviour
{
    public ValueEventSO ValueEvent;
    public float influenceValue = 0;
    public float viewership = 0;
    public int day = 0;

    [Header("情况记录 - 供其他脚本读取某情况是否已发生")]
    [Tooltip("在 Inspector 中预填 Id（如 A、B、C），运行时可通过 SetSituation / GetSituation 读写")]
    public List<SituationFlag> Situations = new List<SituationFlag>();

    private void OnEnable()
    {
        ValueEvent.OnEventRaised += OnValueEventRaised;
    }

    private void OnDisable()
    {
        ValueEvent.OnEventRaised -= OnValueEventRaised;
    }

    private void OnValueEventRaised(int index, float value)
    {
        if (index == 1)
        {
            influenceValue += value;
        }
        else if (index == 2)
        {
            viewership += value;
        }
        else if (index == 3)
        {
            day += (int)value;
        }
    }

    /// <summary> 查询某情况是否已发生；若不存在该 Id 则返回 false </summary>
    public bool GetSituation(string situationId)
    {
        if (Situations == null || string.IsNullOrEmpty(situationId)) return false;
        for (int i = 0; i < Situations.Count; i++)
        {
            if (Situations[i] != null && Situations[i].Id == situationId)
                return Situations[i].Occurred;
        }
        return false;
    }

    /// <summary> 设置某情况是否已发生；若不存在该 Id 则新增一条记录 </summary>
    public void SetSituation(string situationId, bool occurred)
    {
        if (Situations == null) Situations = new List<SituationFlag>();
        for (int i = 0; i < Situations.Count; i++)
        {
            if (Situations[i] != null && Situations[i].Id == situationId)
            {
                Situations[i].Occurred = occurred;
                return;
            }
        }
        Situations.Add(new SituationFlag { Id = situationId, Occurred = occurred });
    }
}
