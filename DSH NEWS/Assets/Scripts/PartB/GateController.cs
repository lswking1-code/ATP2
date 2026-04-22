using UnityEngine;

public class GateController : MonoBehaviour
{
    [Header("Gate Target")]
    [SerializeField] private Transform gatePanel;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Motion")]
    [SerializeField] private bool useRotation = true;
    [SerializeField, Min(0.01f)] private float moveSpeed = 120f;

    [Header("Rotation Mode (Local Euler)")]
    [SerializeField] private Vector3 closedEuler = Vector3.zero;
    [SerializeField] private Vector3 openEuler = new Vector3(0f, 90f, 0f);

    [Header("Position Mode (Local Position)")]
    [SerializeField] private Vector3 closedLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 openLocalPosition = new Vector3(0f, 0f, 1f);

    private bool isOpenTarget;

    private void Awake()
    {
        if (gatePanel == null)
            gatePanel = transform;

        if (moveSpeed <= 0f)
            moveSpeed = 0.01f;

        if (useRotation)
            gatePanel.localRotation = Quaternion.Euler(closedEuler);
        else
            gatePanel.localPosition = closedLocalPosition;

        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null || !triggerCollider.isTrigger)
        {
            Debug.LogWarning($"[{name}] GateController needs a Collider with Is Trigger enabled.");
        }
    }

    private void Update()
    {
        if (gatePanel == null) return;

        if (useRotation)
        {
            Quaternion targetRotation = isOpenTarget
                ? Quaternion.Euler(openEuler)
                : Quaternion.Euler(closedEuler);

            gatePanel.localRotation = Quaternion.RotateTowards(
                gatePanel.localRotation,
                targetRotation,
                moveSpeed * Time.deltaTime
            );
            return;
        }

        Vector3 targetPosition = isOpenTarget ? openLocalPosition : closedLocalPosition;
        gatePanel.localPosition = Vector3.MoveTowards(
            gatePanel.localPosition,
            targetPosition,
            moveSpeed * Time.deltaTime
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        isOpenTarget = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        isOpenTarget = false;
    }
}
