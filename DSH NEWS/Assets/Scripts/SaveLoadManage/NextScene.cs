using UnityEngine;

public class NextScene : MonoBehaviour
{
    public SceneLoadEventSO loadEventSO;
    public GameSceneSO sceneToGo;
    public Vector3 positionToGo;
    public Vector3 rotationToGo;
    public VoidEventSO SwitchScanLineEvent;
    private void Awake()
    {
        SwitchScanLineEvent.RaiseEvent();
    }
    public void OnNextScene()
    {
        if (loadEventSO == null || sceneToGo == null)
        {
            return;
        }
        SwitchScanLineEvent.RaiseEvent();
        loadEventSO.RaiseLoadRequestEvent(sceneToGo, positionToGo, rotationToGo, true);
    }

}
