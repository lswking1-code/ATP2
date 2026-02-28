using UnityEngine;

public class NextScene : MonoBehaviour
{
    public SceneLoadEventSO loadEventSO;
    public GameSceneSO sceneToGo;
    public Vector3 positionToGo;

    public void OnNextScene()
    {
        if (loadEventSO == null || sceneToGo == null)
        {
            return;
        }
        loadEventSO.RaiseLoadRequestEvent(sceneToGo, positionToGo, true);
    }

}
