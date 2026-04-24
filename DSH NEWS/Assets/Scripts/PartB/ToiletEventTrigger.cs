using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ToiletEventTrigger : MonoBehaviour
{
    public enum TriggerType
    {
        NearArea = 0,
        InsideToilet = 1
    }

    [SerializeField] private ToiletEventController controller;
    [SerializeField] private TriggerType triggerType = TriggerType.NearArea;

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
        if (controller == null) return;

        if (triggerType == TriggerType.NearArea)
        {
            controller.NotifyNearAreaEnter(other);
            return;
        }

        controller.NotifyInsideToiletEnter(other);
    }
}
