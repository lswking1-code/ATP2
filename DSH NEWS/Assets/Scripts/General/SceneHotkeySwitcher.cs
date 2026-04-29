using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneHotkeySwitcher : MonoBehaviour
{
    [Header("Hotkey")]
    [SerializeField] private KeyCode switchKey = KeyCode.K;

    [Header("Behavior")]
    [SerializeField] private bool loopToFirstScene = true;

    private void Update()
    {
        if (Input.GetKeyDown(switchKey))
        {
            LoadNextScene();
        }
    }

    private void LoadNextScene()
    {
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        int sceneCount = SceneManager.sceneCountInBuildSettings;

        if (sceneCount <= 0)
        {
            Debug.LogWarning("SceneHotkeySwitcher: Build Settings 中没有可加载场景。", this);
            return;
        }

        int nextIndex = currentIndex + 1;
        if (nextIndex >= sceneCount)
        {
            if (!loopToFirstScene)
            {
                Debug.Log("SceneHotkeySwitcher: 已是最后一个场景。", this);
                return;
            }

            nextIndex = 0;
        }

        SceneManager.LoadScene(nextIndex);
    }
}
