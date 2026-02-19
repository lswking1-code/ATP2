using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "GlitchEventSO", menuName = "Scriptable Objects/GlitchEventSO")]
public class GlitchEventSO : ScriptableObject
{
    public UnityAction <int> OnEventRaised;

    public void RaiseEvent(int index)
    {
        OnEventRaised?.Invoke(index);
    }
}
