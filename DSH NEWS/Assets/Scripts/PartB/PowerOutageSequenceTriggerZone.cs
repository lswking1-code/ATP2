using UnityEngine;

/// <summary>
/// 玩家进入触发区时，通知 PowerOutageSceneSequence 允许开始执行流程。
/// </summary>
[RequireComponent(typeof(Collider))]
public class PowerOutageSequenceTriggerZone : MonoBehaviour
{
    [SerializeField, Tooltip("要激活的断电流程控制器。")]
    private PowerOutageSceneSequence targetSequence;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (targetSequence == null) return;
        targetSequence.NotifyTriggerEnter(other);
    }
}
