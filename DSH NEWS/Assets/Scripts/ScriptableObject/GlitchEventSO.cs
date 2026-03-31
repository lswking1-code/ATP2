using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "GlitchEventSO", menuName = "Scriptable Objects/GlitchEventSO")]
public class GlitchEventSO : ScriptableObject
{
    public UnityAction <int,float> OnEventRaised;

    public void RaiseEvent(int index, float value)
    {
        OnEventRaised?.Invoke(index, value);
    }
}
