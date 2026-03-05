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
    private bool _isSelected;
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

    /// <summary> 由 Manager 调用：设置选中状态，选中时保持在最高点，取消时回到初始位置。 </summary>
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (selected)
        {
            Vector3 localForward = transform.localRotation * Vector3.forward;
            StartMove(initialLocalPosition + localForward * Duration);
        }
        else
        {
            StartMove(initialLocalPosition);
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
        if (_isSelected) return; // 已选中时保持最高点，不重复移动
        Vector3 localForward = transform.localRotation * Vector3.forward;
        StartMove(initialLocalPosition + localForward * Duration);
        OnSelect();
    }

    private void OnMouseExit()
    {
        if (_isSelected) return; // 选中状态下保持最高点，不回到初始位置
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

        // 使用带故障逻辑的接口
        Manager.OnManuscriptSelectedWithGlitch(this);
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
