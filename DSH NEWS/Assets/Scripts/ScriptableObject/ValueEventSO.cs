using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "ValueEventSO", menuName = "Scriptable Objects/ValueEventSO")]
public class ValueEventSO : ScriptableObject
{
    public UnityAction<int,float> OnEventRaised;

    public void RaiseEvent(int index, float value)
    {
        OnEventRaised?.Invoke(index, value);
    }
}
