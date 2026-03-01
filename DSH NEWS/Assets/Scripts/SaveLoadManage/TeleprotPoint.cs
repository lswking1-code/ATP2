using UnityEngine;

public class TeleprotPoint : MonoBehaviour
{
    public SceneLoadEventSO loadEventSO;
    public GameSceneSO sceneToGo;
    public Vector3 positionToGo;

    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (loadEventSO == null || sceneToGo == null)
            {
                return;
            }

            loadEventSO.RaiseLoadRequestEvent(sceneToGo, positionToGo, true);
        }
       
    }
}
