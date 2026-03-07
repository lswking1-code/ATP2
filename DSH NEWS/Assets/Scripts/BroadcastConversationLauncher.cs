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

        Debug.Log($"[Broadcast] Day: {_vm.day}");
        Debug.Log($"[Broadcast] SituationA: {_vm.GetSituation("a")}");
        Debug.Log($"[Broadcast] SituationB: {_vm.GetSituation("b")}");
        Debug.Log($"[Broadcast] SituationC: {_vm.GetSituation("c")}");
        Debug.Log($"[Broadcast] SituationD: {_vm.GetSituation("d")}");

        DialogueLua.SetVariable("SituationA", (bool)_vm.GetSituation("a"));
        DialogueLua.SetVariable("SituationB", (bool)_vm.GetSituation("b"));
        DialogueLua.SetVariable("SituationC", (bool)_vm.GetSituation("c"));
        DialogueLua.SetVariable("SituationD", (bool)_vm.GetSituation("d"));

        Debug.Log($"[Broadcast] Lua SituationA: {DialogueLua.GetVariable("SituationA").asBool}");
        Debug.Log($"[Broadcast] Lua SituationB: {DialogueLua.GetVariable("SituationB").asBool}");
        Debug.Log($"[Broadcast] Lua SituationC: {DialogueLua.GetVariable("SituationC").asBool}");
        Debug.Log($"[Broadcast] Lua SituationD: {DialogueLua.GetVariable("SituationD").asBool}");

        
        string conversationTitle = $"Broadcast_Day{_vm.day}";
        Debug.Log($"[Broadcast] 嘗試啟動 Conversation: {conversationTitle}");

        

        

        DialogueManager.StartConversation(conversationTitle, actor);
        Debug.Log($"[Broadcast] 啟動後 IsConversationActive: {DialogueManager.IsConversationActive}");
    }

    void Update()
    {
        int finished = DialogueLua.GetVariable("FinishedBranch").asInt;
    Debug.Log($"[Broadcast] FinishedBranch: {finished}");
    }
}