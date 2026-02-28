using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "ScriptChangeEventSO", menuName = "Scriptable Objects/ScriptChangeEventSO")]
public class ScriptChangeEventSO : ScriptableObject
{
    public UnityAction<int> OnEventRaised;

    public void RaiseEvent(int index)
    {
        OnEventRaised?.Invoke(index);
    }
}
