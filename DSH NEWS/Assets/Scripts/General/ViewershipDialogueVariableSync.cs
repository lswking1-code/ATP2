using UnityEngine;
using PixelCrushers.DialogueSystem;

/// <summary>
/// 将 <see cref="ValueManage.viewership"/> 与 <see cref="ValueManage.influenceValue"/> 写入 Dialogue System Lua 变量，
/// 供对话节点 conditionsString 使用（变量名须与数据库 Variables 中一致）。
/// </summary>
public class ViewershipDialogueVariableSync : MonoBehaviour
{
    [Tooltip("留空则 FindFirstObjectByType<ValueManage>()")]
    [SerializeField] private ValueManage valueManageOverride;

    [Tooltip("与 Persistent 中 ValueManager 相同的资产；数值变化时刷新 Lua")]
    [SerializeField] private ValueEventSO valueEvent;

    [Tooltip("ValueEvent 中表示收视率变化的 index（与 ValueManage 中 index==2 一致）")]
    [SerializeField] private int viewershipEventIndex = 2;

    [Tooltip("写入 DialogueLua 的收视率变量名")]
    [SerializeField] private string luaVariableName = "Viewership";

    [Tooltip("ValueEvent 中表示影响力变化的 index（与 ValueManage 中 index==1 一致）")]
    [SerializeField] private int influenceEventIndex = 1;

    [Tooltip("写入 DialogueLua 的影响力变量名")]
    [SerializeField] private string influenceLuaVariableName = "Influence";

    private ValueManage _valueManage;

    private void OnEnable()
    {
        if (valueEvent != null)
            valueEvent.OnEventRaised += OnValueEventRaised;

        PushValueManageFieldsToLua();
    }

    private void OnDisable()
    {
        if (valueEvent != null)
            valueEvent.OnEventRaised -= OnValueEventRaised;
    }

    private void Start()
    {
        PushValueManageFieldsToLua();
    }

    private void OnValueEventRaised(int index, float value)
    {
        if (index == viewershipEventIndex || index == influenceEventIndex)
            PushValueManageFieldsToLua();
    }

    private void PushValueManageFieldsToLua()
    {
        ResolveValueManage();
        if (_valueManage == null)
            return;

        if (!string.IsNullOrEmpty(luaVariableName))
            DialogueLua.SetVariable(luaVariableName, _valueManage.viewership);

        if (!string.IsNullOrEmpty(influenceLuaVariableName))
            DialogueLua.SetVariable(influenceLuaVariableName, _valueManage.influenceValue);
    }

    private void ResolveValueManage()
    {
        if (valueManageOverride != null)
        {
            _valueManage = valueManageOverride;
            return;
        }

        if (_valueManage == null)
            _valueManage = FindFirstObjectByType<ValueManage>();
    }
}
