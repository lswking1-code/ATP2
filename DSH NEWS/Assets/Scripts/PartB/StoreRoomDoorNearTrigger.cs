using UnityEngine;

/// <summary>
/// 玩家进入近距触发区时通知 <see cref="StoreRoomDoor"/> 执行关门、近距关门声与房内诡异声（关门声在 StoreRoomDoor 上配置 nearTriggerCloseDoorClip）。
/// </summary>
[RequireComponent(typeof(Collider))]
public class StoreRoomDoorNearTrigger : MonoBehaviour
{
    [SerializeField, Tooltip("关联的储物室剧情门控制器（近距关门声、诡异声在 StoreRoomDoor 上配）")]
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
