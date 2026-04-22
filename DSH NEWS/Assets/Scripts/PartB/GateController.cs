using UnityEngine;

public class GateController : MonoBehaviour
{
    [Header("Gate Target")]
    [SerializeField] private Transform[] gatePanels;

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
    private int playerInsideCount;

    private void Awake()
    {
        if (gatePanels == null || gatePanels.Length == 0)
            gatePanels = new[] { transform };

        if (moveSpeed <= 0f)
            moveSpeed = 0.01f;

        SetAllGatesToClosedStateImmediately();

        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null || !triggerCollider.isTrigger)
        {
            Debug.LogWarning($"[{name}] GateController needs a Collider with Is Trigger enabled.");
        }
    }

    private void Update()
    {
        if (gatePanels == null || gatePanels.Length == 0) return;

        for (int i = 0; i < gatePanels.Length; i++)
        {
            Transform gatePanel = gatePanels[i];
            if (gatePanel == null) continue;

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
                continue;
            }

            Vector3 targetPosition = isOpenTarget ? openLocalPosition : closedLocalPosition;
            gatePanel.localPosition = Vector3.MoveTowards(
                gatePanel.localPosition,
                targetPosition,
                moveSpeed * Time.deltaTime
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        playerInsideCount++;
        isOpenTarget = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        playerInsideCount = Mathf.Max(0, playerInsideCount - 1);
        isOpenTarget = playerInsideCount > 0;
    }

    private void SetAllGatesToClosedStateImmediately()
    {
        if (gatePanels == null) return;

        for (int i = 0; i < gatePanels.Length; i++)
        {
            Transform gatePanel = gatePanels[i];
            if (gatePanel == null) continue;

            if (useRotation)
                gatePanel.localRotation = Quaternion.Euler(closedEuler);
            else
                gatePanel.localPosition = closedLocalPosition;
        }
    }
}
