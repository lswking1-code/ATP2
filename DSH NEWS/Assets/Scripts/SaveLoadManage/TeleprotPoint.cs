using UnityEngine;

public class TeleprotPoint : MonoBehaviour, IInteractable
{
    public SceneLoadEventSO loadEventSO;
    public GameSceneSO sceneToGo;
    public Vector3 positionToGo;

    public void OnInteract()
    {
        if (loadEventSO == null || sceneToGo == null)
        {
            return;
        }

        loadEventSO.RaiseLoadRequestEvent(sceneToGo, positionToGo, true);
    }
}
