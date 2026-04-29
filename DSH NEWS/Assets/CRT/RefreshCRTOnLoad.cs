using UnityEngine;
using UnityEngine.SceneManagement;

public class RefreshCRTOnLoad : MonoBehaviour
{
    public Material crtMaterial;
  

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (crtMaterial != null)
        {
            // 强制刷新所有参数，确保和当前相机状态同步
            crtMaterial.SetFloat("_DummyParam", Random.value);
            crtMaterial.SetPass(0);
        }

      
    }
}
