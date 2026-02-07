using UnityEngine;
public class Manuscript : MonoBehaviour
{
    [Header("Select Effect")]
    [Range(0f, 10f)]
    public float Duration = 0.25f;
    [Range(0.01f, 10f)]
    public float Speed = 0.25f;

    [HideInInspector]
    public TextAsset TextFile;
    public ManuscriptManager Manager;

    private Vector3 initialLocalPosition;
    private Coroutine moveRoutine;
    [Header("EventRaiser")]
    public ValueEventSO ValueEvent;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
        if (Manager == null)
        {
            Manager = FindFirstObjectByType<ManuscriptManager>();
        }
    }

    public void SetTextFile(TextAsset textFile)
    {
        TextFile = textFile;
    }

    public void SetManager(ManuscriptManager manager)
    {
        Manager = manager;
    }

    private void OnMouseEnter()
    {
        Vector3 localForward = transform.localRotation * Vector3.forward;
        StartMove(initialLocalPosition + localForward * Duration);
        OnSelect();
    }

    private void OnMouseExit()
    {
        StartMove(initialLocalPosition);
    }
    private void OnMouseDown()
    {
        OnClick();
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
    private void OnSelect()
    {
        if (Manager == null)
        {
            Debug.LogWarning("Manuscript: Manager is not assigned.", this);
            return;
        }

        Manager.OnManuscriptSelected(this);
    }
    private void OnClick()
    {
        if (Manager == null)
        {
            Debug.LogWarning("Manuscript: Manager is not assigned.", this);
            return;
        }

        Manager.OnManuscriptClicked(this);
    }
}
