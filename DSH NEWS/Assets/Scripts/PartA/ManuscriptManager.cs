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
    public Vector3 RotationToGo;
    [Header("Glitch Settings")]
    public float GlitchTextDelay = 1f;
    [Header("Manuscripts - 按 day 切换使用的组")]
    public List<EntryGroup> EntryGroups = new List<EntryGroup>();
    
    public int MaxSelectionCount = 2;
    private List<ManuscriptEntry> _activeEntries = new List<ManuscriptEntry>();
    private HashSet<Manuscript> _selectedManuscripts = new HashSet<Manuscript>();
    private HashSet<Manuscript> _glitchTriggeredManuscripts = new HashSet<Manuscript>();
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
            if (valueManage.Situations != null)
            {
                valueManage.Situations.Clear();
            }
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

        // 切换选中状态：已在选中列表中则取消，否则加入选中
        if (_selectedManuscripts.Contains(manuscript))
        {
            _selectedManuscripts.Remove(manuscript);
            manuscript.SetSelected(false);
            return;
        }

        _selectedManuscripts.Add(manuscript);
        manuscript.SetSelected(true);

        if (ScriptChangeEvent != null && entry.ScriptIndex != 0)
        {
            ScriptChangeEvent.RaiseEvent(entry.ScriptIndex);
        }

        // 仅当选中数量达到 MaxSelectionCount 时才载入下一场景
        if (_selectedManuscripts.Count >= MaxSelectionCount && SceneToGo != null && SceneLoadEvent != null)
        {
            if (valueManage != null)
            {
                foreach (Manuscript selectedManuscript in _selectedManuscripts)
                {
                    ManuscriptEntry selectedEntry = FindEntryByManuscript(selectedManuscript);
                    if (selectedEntry == null)
                    {
                        continue;
                    }

                    if (ValueEvent != null && selectedEntry.Values != null && selectedEntry.Values.Count > 0)
                    {
                        for (int i = 0; i < selectedEntry.Values.Count; i++)
                        {
                            ValueChange v = selectedEntry.Values[i];
                            ValueEvent.RaiseEvent(v.ValueIndex, v.ValueAmount);
                        }
                    }

                    if (!string.IsNullOrEmpty(selectedEntry.Id))
                    {
                        valueManage.SetSituation(selectedEntry.Id, true);
                    }
                }
            }
            SceneLoadEvent.RaiseLoadRequestEvent(SceneToGo, PositionToGo, RotationToGo, true);
        }
    }

    private ManuscriptEntry FindEntryByManuscript(Manuscript manuscript)
    {
        for (int i = 0; i < _activeEntries.Count; i++)
        {
            if (_activeEntries[i] != null && _activeEntries[i].Manuscript == manuscript)
            {
                return _activeEntries[i];
            }
        }
        return null;
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

        // 已触发过 Glitch 的稿件：悬停时直接显示故障文本，不再播放动画
        if (_glitchTriggeredManuscripts.Contains(manuscript))
        {
            if (entry.GlitchTextFile != null)
            {
                Text.text = entry.GlitchTextFile.text;
            }
            else if (entry.TextFile != null)
            {
                Text.text = entry.TextFile.text;
            }
            else if (manuscript.TextFile != null)
            {
                Text.text = manuscript.TextFile.text;
            }
            return;
        }

        // 停掉之前的故障流程
        if (glitchRoutine != null)
        {
            StopCoroutine(glitchRoutine);
            glitchRoutine = null;
        }

        // 有故障的 Manuscript：仅首次触发——先显示正常文本，再播放故障动画，最后换成故障文本
        if (entry.HasGlitch && GlitchEvent != null && entry.GlitchTextFile != null)
        {
            glitchRoutine = StartCoroutine(HandleGlitchSelection(entry, manuscript));
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

    private IEnumerator HandleGlitchSelection(ManuscriptEntry entry, Manuscript manuscript)
    {
        // 先显示原始文本
        if (entry.TextFile != null)
        {
            Text.text = entry.TextFile.text;
        }

        // 触发 Glitch2 动画（仅此一次）
        GlitchEvent.RaiseEvent(2, 0f);

        // 等待动画结束（在 Inspector 中通过 GlitchTextDelay 调整，需与动画长度一致）
        if (GlitchTextDelay > 0f)
        {
            yield return new WaitForSeconds(GlitchTextDelay);
        }

        // 动画结束后显示故障文本，并标记该稿件已触发过 Glitch
        if (entry.GlitchTextFile != null)
        {
            Text.text = entry.GlitchTextFile.text;
        }
        _glitchTriggeredManuscripts.Add(manuscript);

        glitchRoutine = null;
    }
}
