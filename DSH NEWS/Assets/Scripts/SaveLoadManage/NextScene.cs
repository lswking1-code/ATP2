using UnityEngine;

public class NextScene : MonoBehaviour
{
    public SceneLoadEventSO loadEventSO;
    public GameSceneSO[] sceneToGo;
    public Vector3 positionToGo;

    public void OnNextScene()
    {
        if (loadEventSO == null || sceneToGo.Length == 0)
        {
            return;
        }
        loadEventSO.RaiseLoadRequestEvent(sceneToGo[0], positionToGo, true);
    }

}
