using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "SceneUnloadEventSO", menuName = "Scriptable Objects/SceneUnloadEventSO")]
public class SceneUnloadEventSO : ScriptableObject
{
    public UnityAction<string> OnEventRaised;

    public void RaiseEvent(string sceneName)
    {
        OnEventRaised?.Invoke(sceneName);
    }
}
