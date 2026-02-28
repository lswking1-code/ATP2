using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class EntryGroup
{
    [Tooltip("该组命名，便于在 Inspector 中区分")]
    public string Name = "New Group";
    [Tooltip("当 ValueManage.day 等于此值时使用本组 Entries")]
    public int Day = 0;
    public List<ManuscriptEntry> Entries = new List<ManuscriptEntry>();
}

public class ManuscriptManager : MonoBehaviour
{
    public TextMeshProUGUI Text;
    public ValueEventSO ValueEvent;
    public GlitchEventSO GlitchEvent;
    public ScriptChangeEventSO ScriptChangeEvent;
    [Header("Scene Load")]
    public SceneLoadEventSO SceneLoadEvent;
    public Vector3 PositionToGo;
    public GameSceneSO SceneToGo;
    [Header("Glitch Settings")]
    public float GlitchTextDelay = 1f;
    [Header("Manuscripts - 按 day 切换使用的组")]
    public List<EntryGroup> EntryGroups = new List<EntryGroup>();

    private List<ManuscriptEntry> _activeEntries = new List<ManuscriptEntry>();
    private Coroutine glitchRoutine;
    private ValueManage valueManage;
    private void Awake()
    {
        ValueEvent.RaiseEvent(3, 1);
    }

    private void Start()
    {
        int day = 0;
        valueManage = FindFirstObjectByType<ValueManage>();
        if (valueManage != null)
        {
            day = valueManage.day;
        }

        EntryGroup selected = null;
        for (int i = 0; i < EntryGroups.Count; i++)
        {
            if (EntryGroups[i] != null && EntryGroups[i].Day == day)
            {
                selected = EntryGroups[i];
                break;
            }
        }

        if (selected != null)
        {
            _activeEntries = selected.Entries ?? new List<ManuscriptEntry>();
        }
        else if (EntryGroups.Count > 0 && EntryGroups[0] != null)
        {
            _activeEntries = EntryGroups[0].Entries ?? new List<ManuscriptEntry>();
            Debug.LogWarning($"ManuscriptManager: 未找到 day={day} 的 EntryGroup，使用第一组 \"{EntryGroups[0].Name}\"。", this);
        }
        else
        {
            _activeEntries = new List<ManuscriptEntry>();
        }

        AssignTextFiles(_activeEntries);
    }

    private void OnValidate()
    {
        for (int g = 0; g < EntryGroups.Count; g++)
        {
            if (EntryGroups[g] != null && EntryGroups[g].Entries != null)
            {
                AssignTextFiles(EntryGroups[g].Entries);
            }
        }
    }

    private void AssignTextFiles(List<ManuscriptEntry> entries)
    {
        if (entries == null) return;
        for (int i = 0; i < entries.Count; i++)
        {
            ManuscriptEntry entry = entries[i];
            if (entry == null || entry.Manuscript == null)
            {
                continue;
            }

            entry.Manuscript.SetManager(this);
            entry.Manuscript.SetTextFile(entry.TextFile);
        }
    }

    public void OnManuscriptSelected(Manuscript manuscript)
    {
        if (manuscript == null)
        {
            Debug.LogWarning("ManuscriptManager: Manuscript is null.", this);
            return;
        }

        ManuscriptEntry entry = null;
        for (int i = 0; i < _activeEntries.Count; i++)
        {
            if (_activeEntries[i] != null && _activeEntries[i].Manuscript == manuscript)
            {
                entry = _activeEntries[i];
                break;
            }
        }

        /*if (entry == null)
        {
            Debug.LogWarning("ManuscriptManager: Manuscript not registered.", manuscript);
            return;
        }

        if (Text == null)
        {
            Debug.LogWarning("ManuscriptManager: Text is not assigned.", this);
            return;
        }

        if (manuscript.TextFile == null || string.IsNullOrEmpty(manuscript.TextFile.text))
        {
            Debug.LogWarning("ManuscriptManager: No content loaded from TXT.", manuscript);
            return;
        }*/

        Text.text = manuscript.TextFile.text;
    }
    public void OnManuscriptClicked(Manuscript manuscript)
    {
        if (manuscript == null)
        {
            Debug.LogWarning("ManuscriptManager: Manuscript is null.", this);
            return;
        }

        ManuscriptEntry entry = null;
        for (int i = 0; i < _activeEntries.Count; i++)
        {
            if (_activeEntries[i] != null && _activeEntries[i].Manuscript == manuscript)
            {
                entry = _activeEntries[i];
                break;
            }
        }

        if (entry == null)
        {
            Debug.LogWarning("ManuscriptManager: Manuscript not registered.", manuscript);
            return;
        }

        if (ValueEvent == null)
        {
            Debug.LogWarning("ManuscriptManager: ValueEvent is not assigned.", this);
            return;
        }

        // 支持多个 Value：优先使用 Values 列表，为空时使用单值（兼容旧配置）
        if (entry.Values != null && entry.Values.Count > 0)
        {
            for (int i = 0; i < entry.Values.Count; i++)
            {
                ValueChange v = entry.Values[i];
                ValueEvent.RaiseEvent(v.ValueIndex, v.ValueAmount);
            }
        }
        else
        {
            //ValueEvent.RaiseEvent(entry.ValueIndex, entry.ValueAmount);
        }
        if (entry.Id != string.Empty)
        {
            valueManage.SetSituation(entry.Id, true);
        }
        if (entry.ScriptIndex != 0)
        {
            ScriptChangeEvent.RaiseEvent(entry.ScriptIndex);
        }
        if (SceneToGo != null)
        {
            SceneLoadEvent.RaiseLoadRequestEvent(SceneToGo, PositionToGo, true);
        }
    }

    // 带故障逻辑的选中接口，供 Manuscript 调用
    public void OnManuscriptSelectedWithGlitch(Manuscript manuscript)
    {
        if (manuscript == null)
        {
            Debug.LogWarning("ManuscriptManager: Manuscript is null.", this);
            return;
        }

        ManuscriptEntry entry = null;
        for (int i = 0; i < _activeEntries.Count; i++)
        {
            if (_activeEntries[i] != null && _activeEntries[i].Manuscript == manuscript)
            {
                entry = _activeEntries[i];
                break;
            }
        }

        if (entry == null)
        {
            Debug.LogWarning("ManuscriptManager: Manuscript not registered.", manuscript);
            return;
        }

        if (Text == null)
        {
            Debug.LogWarning("ManuscriptManager: Text is not assigned.", this);
            return;
        }

        // 停掉之前的故障流程
        if (glitchRoutine != null)
        {
            StopCoroutine(glitchRoutine);
            glitchRoutine = null;
        }

        // 有故障的 Manuscript：先显示正常文本，再播放故障动画，最后换成故障文本
        if (entry.HasGlitch && GlitchEvent != null && entry.GlitchTextFile != null)
        {
            glitchRoutine = StartCoroutine(HandleGlitchSelection(entry));
        }
        else
        {
            if (entry.TextFile != null)
            {
                Text.text = entry.TextFile.text;
            }
            else if (manuscript.TextFile != null)
            {
                Text.text = manuscript.TextFile.text;
            }
            else
            {
                Debug.LogWarning("ManuscriptManager: No TextFile assigned for this Manuscript.", manuscript);
            }
        }
    }

    private IEnumerator HandleGlitchSelection(ManuscriptEntry entry)
    {
        // 先显示原始文本
        if (entry.TextFile != null)
        {
            Text.text = entry.TextFile.text;
        }

        // 触发 Glitch2 动画
        GlitchEvent.RaiseEvent(2);

        // 等待动画结束（在 Inspector 中通过 GlitchTextDelay 调整，需与动画长度一致）
        if (GlitchTextDelay > 0f)
        {
            yield return new WaitForSeconds(GlitchTextDelay);
        }

        // 动画结束后显示故障文本
        if (entry.GlitchTextFile != null)
        {
            Text.text = entry.GlitchTextFile.text;
        }

        glitchRoutine = null;
    }
}
