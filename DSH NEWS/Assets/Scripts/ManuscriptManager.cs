using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ManuscriptManager : MonoBehaviour
{
    public TextMeshProUGUI Text;
    public ValueEventSO ValueEvent;
    public GlitchEventSO GlitchEvent;
    [Header("Glitch Settings")]
    public float GlitchTextDelay = 1f;
    [Header("Manuscripts")]
    public List<ManuscriptEntry> Entries = new List<ManuscriptEntry>();

    private Coroutine glitchRoutine;

    private void Awake()
    {
        AssignTextFiles();
    }

    private void OnValidate()
    {
        AssignTextFiles();
    }

    private void AssignTextFiles()
    {
        for (int i = 0; i < Entries.Count; i++)
        {
            ManuscriptEntry entry = Entries[i];
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
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i] != null && Entries[i].Manuscript == manuscript)
            {
                entry = Entries[i];
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
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i] != null && Entries[i].Manuscript == manuscript)
            {
                entry = Entries[i];
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

        ValueEvent.RaiseEvent(entry.ValueIndex, entry.ValueAmount);
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
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i] != null && Entries[i].Manuscript == manuscript)
            {
                entry = Entries[i];
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
