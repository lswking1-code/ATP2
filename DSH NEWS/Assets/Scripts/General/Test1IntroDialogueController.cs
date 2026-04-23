using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;

[System.Serializable]
public class DayConversationEntry
{
    public int Day = 1;
    public string ConversationTitle = "";
}

public class Test1IntroDialogueController : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] private Transform actor;
    [SerializeField] private List<DayConversationEntry> dayConversations = new List<DayConversationEntry>();
    [SerializeField] private bool usePatternFallback = true;
    [SerializeField] private string fallbackPattern = "S1_Day{0}";
    [SerializeField] private string defaultConversationTitle = "";

    [Header("Scene Gating")]
    [SerializeField] private GameObject papersRoot;

    private bool _isListening;
    private bool _isIntroRunning;

    private void Start()
    {
        StartIntroConversation();
    }

    private void OnDisable()
    {
        UnregisterDialogueCallbacks();
    }

    private void StartIntroConversation()
    {
        string conversationTitle = ResolveConversationTitle();
        if (string.IsNullOrEmpty(conversationTitle))
        {
            Debug.LogWarning("Test1IntroDialogueController: No conversation title resolved.", this);
            return;
        }

        if (papersRoot != null)
        {
            papersRoot.SetActive(false);
        }

        RegisterDialogueCallbacks();
        DialogueManager.StartConversation(conversationTitle, actor);
        _isIntroRunning = true;

        // If conversation fails to start, recover Papers immediately.
        if (!DialogueManager.IsConversationActive)
        {
            FinishIntroConversation();
        }
    }

    private string ResolveConversationTitle()
    {
        int day = 0;
        ValueManage valueManage = FindFirstObjectByType<ValueManage>();
        if (valueManage != null)
        {
            day = valueManage.day;
        }

        for (int i = 0; i < dayConversations.Count; i++)
        {
            DayConversationEntry entry = dayConversations[i];
            if (entry != null && entry.Day == day && !string.IsNullOrEmpty(entry.ConversationTitle))
            {
                return entry.ConversationTitle;
            }
        }

        if (usePatternFallback && !string.IsNullOrEmpty(fallbackPattern))
        {
            return string.Format(fallbackPattern, day);
        }

        return defaultConversationTitle;
    }

    private void RegisterDialogueCallbacks()
    {
        if (_isListening || DialogueManager.instance == null)
        {
            return;
        }

        DialogueManager.instance.conversationEnded += OnConversationEnded;
        _isListening = true;
    }

    private void UnregisterDialogueCallbacks()
    {
        if (!_isListening || DialogueManager.instance == null)
        {
            return;
        }

        DialogueManager.instance.conversationEnded -= OnConversationEnded;
        _isListening = false;
    }

    private void OnConversationEnded(Transform _)
    {
        FinishIntroConversation();
    }

    private void FinishIntroConversation()
    {
        if (!_isIntroRunning)
        {
            return;
        }

        _isIntroRunning = false;

        if (papersRoot != null)
        {
            papersRoot.SetActive(true);
        }

        UnregisterDialogueCallbacks();
    }
}
