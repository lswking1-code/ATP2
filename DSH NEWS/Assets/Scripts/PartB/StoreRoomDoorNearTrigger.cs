using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StoreRoomDoorNearTrigger : MonoBehaviour
{
    [SerializeField, Tooltip("关联的储物室剧情门控制器")]
    private StoreRoomDoor storeRoomDoor;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (storeRoomDoor == null)
        {
            return;
        }

        storeRoomDoor.NotifyNearDoorPlayerEnter(other);
    }
}
