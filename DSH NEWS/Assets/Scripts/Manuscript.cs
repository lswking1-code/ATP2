using UnityEngine;

public class Manuscript : MonoBehaviour
{
    [Range(0f, 10f)]
    public float Duration = 0.25f;
    [Range(0.01f, 10f)]
    public float Speed = 0.25f;

    private Vector3 initialLocalPosition;
    private Coroutine moveRoutine;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
    }

    private void OnMouseEnter()
    {
        Vector3 localForward = transform.localRotation * Vector3.forward;
        StartMove(initialLocalPosition + localForward * Duration);
    }

    private void OnMouseExit()
    {
        StartMove(initialLocalPosition);
    }

    private void StartMove(Vector3 targetPosition)
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }

        moveRoutine = StartCoroutine(MoveTo(targetPosition, Speed));
    }

    private System.Collections.IEnumerator MoveTo(Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = transform.localPosition;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            transform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        transform.localPosition = targetPosition;
    }
}
