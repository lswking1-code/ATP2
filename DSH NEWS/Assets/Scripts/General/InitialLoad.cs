using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

/// <summary>
/// 启动引导器：确保 Persistent 场景先被加载。
/// 建议将本脚本挂在一个专用启动场景（Bootstrap）中。
/// </summary>
public class InitialLoad : MonoBehaviour
{
    [Header("Bootstrap")]
    [Tooltip("Persistent 场景的 Addressable 引用（例如 Assets/Scenes/Persistent.unity）")]
    [SerializeField] private AssetReference persistentScene;

    [Tooltip("Persistent 加载后是否自动卸载当前启动场景")]
    [SerializeField] private bool unloadBootstrapSceneAfterLoaded = true;

    private static bool s_hasBootstrapped;
    private bool _loading;

    private void Awake()
    {
        if (s_hasBootstrapped)
        {
            return;
        }

        StartCoroutine(BootstrapRoutine());
    }

    private IEnumerator BootstrapRoutine()
    {
        if (_loading)
        {
            yield break;
        }

        _loading = true;

        if (persistentScene == null || !persistentScene.RuntimeKeyIsValid())
        {
            Debug.LogError("InitialLoad: persistentScene is not assigned or invalid.", this);
            _loading = false;
            yield break;
        }

        AsyncOperationHandle<SceneInstance> handle =
            Addressables.LoadSceneAsync(persistentScene, LoadSceneMode.Additive, true);

        yield return handle;

        _loading = false;

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError("InitialLoad: Failed to load Persistent scene.", this);
            yield break;
        }

        Debug.Log($"[InitialLoad] Scene loaded: {handle.Result.Scene.name}");

        s_hasBootstrapped = true;

        if (!unloadBootstrapSceneAfterLoaded)
        {
            yield break;
        }

        Scene bootstrapScene = gameObject.scene;
        // 避免把唯一场景卸掉：只有当已加载场景数 > 1 时才卸载启动场景。
        if (bootstrapScene.IsValid() && SceneManager.sceneCount > 1)
        {
            SceneManager.UnloadSceneAsync(bootstrapScene);
        }
    }
}
