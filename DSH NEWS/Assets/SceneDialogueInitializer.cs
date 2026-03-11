using UnityEngine;
using PixelCrushers.DialogueSystem;
using System.Collections;

public class SceneDialogueInitializer : MonoBehaviour {

     [SerializeField] private DialogueSystemTrigger dialogueTrigger;
    void Awake() {
        // 停用跟過來的舊 UI
        var oldUI = DialogueManager.instance.GetComponentInChildren<StandardDialogueUI>(true);
        if (oldUI != null) oldUI.gameObject.SetActive(false);

            Debug.Log("DialogueManager exists: " + (DialogueManager.instance != null));

            

    }

    IEnumerator Start() {
        yield return null;
        dialogueTrigger.OnTriggerEnter(null); // 改用這個
    }

    void OnDestroy() {
        // 離開場景 D 時，把舊 UI 重新啟用
        var oldUI = DialogueManager.instance.GetComponentInChildren<StandardDialogueUI>(true);
        if (oldUI != null) oldUI.gameObject.SetActive(true);
    }
}


