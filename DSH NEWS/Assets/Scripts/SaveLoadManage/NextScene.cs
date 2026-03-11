using UnityEngine;

public class NextScene : MonoBehaviour
{
    public SceneLoadEventSO loadEventSO;
    public GameSceneSO sceneToGo;
    public Vector3 positionToGo;
    public Vector3 rotationToGo;

    public void OnNextScene()
    {
        if (loadEventSO == null || sceneToGo == null)
        {
            return;
        }
        loadEventSO.RaiseLoadRequestEvent(sceneToGo, positionToGo, rotationToGo, true);
    }

}
