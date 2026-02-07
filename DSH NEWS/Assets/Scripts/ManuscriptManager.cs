using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class ManuscriptManager : MonoBehaviour
{
    public TextMeshProUGUI Text;
    public ValueEventSO ValueEvent;
    [Header("Manuscripts")]
    public List<ManuscriptEntry> Entries = new List<ManuscriptEntry>();

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

        if (manuscript.TextFile == null || string.IsNullOrEmpty(manuscript.TextFile.text))
        {
            Debug.LogWarning("ManuscriptManager: No content loaded from TXT.", manuscript);
            return;
        }

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
}
