using UnityEngine;

public class GateController : MonoBehaviour
{
    private enum OpenSide
    {
        Left = -1,
        Right = 1
    }

    [System.Serializable]
    private class GateLeaf
    {
        public Transform panel;
        public OpenSide openSide = OpenSide.Right;
    }

    [Header("Gate Target")]
    [SerializeField] private GateLeaf[] gates;

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
        if (gates == null || gates.Length == 0)
        {
            gates = new[]
            {
                new GateLeaf { panel = transform, openSide = OpenSide.Right }
            };
        }

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
        if (gates == null || gates.Length == 0) return;

        for (int i = 0; i < gates.Length; i++)
        {
            GateLeaf gate = gates[i];
            if (gate == null) continue;

            Transform gatePanel = gate.panel;
            if (gatePanel == null) continue;

            float directionSign = (float)gate.openSide;

            if (useRotation)
            {
                Vector3 signedOpenEuler = new Vector3(
                    openEuler.x,
                    openEuler.y * directionSign,
                    openEuler.z
                );

                Quaternion targetRotation = isOpenTarget
                    ? Quaternion.Euler(signedOpenEuler)
                    : Quaternion.Euler(closedEuler);

                gatePanel.localRotation = Quaternion.RotateTowards(
                    gatePanel.localRotation,
                    targetRotation,
                    moveSpeed * Time.deltaTime
                );
                continue;
            }

            Vector3 signedOpenLocalPosition = new Vector3(
                openLocalPosition.x * directionSign,
                openLocalPosition.y,
                openLocalPosition.z
            );

            Vector3 targetPosition = isOpenTarget ? signedOpenLocalPosition : closedLocalPosition;
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
        if (gates == null) return;

        for (int i = 0; i < gates.Length; i++)
        {
            GateLeaf gate = gates[i];
            if (gate == null) continue;

            Transform gatePanel = gate.panel;
            if (gatePanel == null) continue;

            if (useRotation)
                gatePanel.localRotation = Quaternion.Euler(closedEuler);
            else
                gatePanel.localPosition = closedLocalPosition;
        }
    }
}
