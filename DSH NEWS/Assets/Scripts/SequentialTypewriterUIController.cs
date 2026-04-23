using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SequentialTypewriterUIController : MonoBehaviour
{
    [Header("Branch Content Data")]
    [Tooltip("Used when viewership <= X.")]
    [SerializeField] private List<string> branchAContentList = new List<string>();
    [Tooltip("Used when viewership > X and influence < Y.")]
    [SerializeField] private List<string> branchBContentList = new List<string>();
    [Tooltip("Used when viewership > X and influence >= Y.")]
    [SerializeField] private List<string> branchCContentList = new List<string>();
    [Tooltip("TMP text targets used in order.")]
    [SerializeField] private List<TMP_Text> textUiList = new List<TMP_Text>();

    [Header("Branch Rules")]
    [Tooltip("Threshold X: if viewership <= X, branch A is selected.")]
    [SerializeField] private float viewershipThresholdX = 0f;
    [Tooltip("Threshold Y: if viewership > X, influence < Y goes branch B; otherwise branch C.")]
    [SerializeField] private float influenceThresholdY = 0f;
    [Tooltip("Reference to ValueManage for reading viewership/influence values.")]
    [SerializeField] private ValueManage valueManage;

    [Header("Timing")]
    [Tooltip("Delay between each character while typing.")]
    [Min(0f)]
    [SerializeField] private float charIntervalSeconds = 0.05f;
    [Tooltip("Delay after each content item completes.")]
    [Min(0f)]
    [SerializeField] private float itemIntervalSeconds = 1f;

    [Header("Game End UI")]
    [Tooltip("CanvasGroup to show when all content has finished.")]
    [SerializeField] private CanvasGroup gameEndCanvasGroup;

    private Coroutine _playRoutine;
    private List<string> _activeContentList;

    private void Start()
    {
        ResolveValueManageReference();
        _activeContentList = SelectContentBranch();

        if (!HasValidInput())
        {
            ShowGameEndUI();
            return;
        }

        HideGameEndUI();
        ClearAllTextUIs();
        _playRoutine = StartCoroutine(PlaySequenceCoroutine());
    }

    private bool HasValidInput()
    {
        if (_activeContentList == null || _activeContentList.Count == 0)
        {
            Debug.LogWarning("SequentialTypewriterUIController: selected content branch is empty.");
            return false;
        }

        if (textUiList == null || textUiList.Count == 0)
        {
            Debug.LogWarning("SequentialTypewriterUIController: textUiList is empty.");
            return false;
        }

        return true;
    }

    private void ResolveValueManageReference()
    {
        if (valueManage != null)
        {
            return;
        }

        valueManage = FindObjectOfType<ValueManage>();
        if (valueManage == null)
        {
            Debug.LogWarning("SequentialTypewriterUIController: ValueManage not found. Fallback to branch A.");
        }
    }

    private List<string> SelectContentBranch()
    {
        if (valueManage == null)
        {
            return branchAContentList;
        }

        float viewership = valueManage.viewership;
        float influence = valueManage.influenceValue;

        if (viewership <= viewershipThresholdX)
        {
            return branchAContentList;
        }

        if (influence < influenceThresholdY)
        {
            return branchBContentList;
        }

        return branchCContentList;
    }

    private IEnumerator PlaySequenceCoroutine()
    {
        int contentIndex = 0;
        int uiIndex = 0;

        while (contentIndex < _activeContentList.Count)
        {
            if (uiIndex >= textUiList.Count)
            {
                ClearAllTextUIs();
                uiIndex = 0;
            }

            TMP_Text targetText = textUiList[uiIndex];
            if (targetText == null)
            {
                Debug.LogWarning("SequentialTypewriterUIController: Found null TMP_Text in textUiList, skipping this slot.");
                uiIndex++;
                continue;
            }

            string content = _activeContentList[contentIndex] ?? string.Empty;
            yield return StartCoroutine(TypeIntoTextCoroutine(targetText, content));

            contentIndex++;
            uiIndex++;

            if (contentIndex < _activeContentList.Count && itemIntervalSeconds > 0f)
            {
                yield return new WaitForSeconds(itemIntervalSeconds);
            }
        }

        _playRoutine = null;
        ShowGameEndUI();
    }

    private IEnumerator TypeIntoTextCoroutine(TMP_Text target, string content)
    {
        target.text = string.Empty;

        for (int i = 0; i < content.Length; i++)
        {
            target.text += content[i];

            if (charIntervalSeconds > 0f)
            {
                yield return new WaitForSeconds(charIntervalSeconds);
            }
        }
    }

    private void ClearAllTextUIs()
    {
        for (int i = 0; i < textUiList.Count; i++)
        {
            TMP_Text textUi = textUiList[i];
            if (textUi != null)
            {
                textUi.text = string.Empty;
            }
        }
    }

    private void HideGameEndUI()
    {
        if (gameEndCanvasGroup == null)
        {
            return;
        }

        gameEndCanvasGroup.alpha = 0f;
        gameEndCanvasGroup.interactable = false;
        gameEndCanvasGroup.blocksRaycasts = false;
    }

    private void ShowGameEndUI()
    {
        if (gameEndCanvasGroup == null)
        {
            return;
        }

        if (!gameEndCanvasGroup.gameObject.activeSelf)
        {
            gameEndCanvasGroup.gameObject.SetActive(true);
        }

        gameEndCanvasGroup.alpha = 1f;
        gameEndCanvasGroup.interactable = true;
        gameEndCanvasGroup.blocksRaycasts = true;
    }
}
