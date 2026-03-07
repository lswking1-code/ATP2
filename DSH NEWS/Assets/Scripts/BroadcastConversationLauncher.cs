using UnityEngine;
using PixelCrushers.DialogueSystem;

public class BroadcastConversationLauncher : MonoBehaviour
{
    [Header("對話主角（可留 null）")]
    public Transform actor;

    private ValueManage _vm;

    private void Start()
    {
        _vm = FindObjectOfType<ValueManage>();

        if (_vm == null)
        {
            Debug.LogError("找不到 ValueManage！");
            return;
        }

        DialogueLua.SetVariable("SituationA", _vm.GetSituation("a"));
        DialogueLua.SetVariable("SituationB", _vm.GetSituation("b"));
        DialogueLua.SetVariable("SituationC", _vm.GetSituation("c"));
        DialogueLua.SetVariable("SituationD", _vm.GetSituation("d"));

        string conversationTitle = $"Broadcast_Day{_vm.day}";
        DialogueManager.StartConversation(conversationTitle, actor);
    }
}