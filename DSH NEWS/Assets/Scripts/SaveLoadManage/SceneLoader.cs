using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SceneLoader : MonoBehaviour, ISaveable
{
    public Transform playerTrans;
    public Vector3 firstPosition;
    public Vector3 menuPosition;

    [Header("Camera")]
    [Tooltip("常驻在 Persistent 场景的备用相机，在过渡期或 Menu 时保证始终有相机渲染，避免 \"No cameras rendering\"")]
    public Camera fallbackCamera;

    [Header("Event Listeners")]
    public SceneLoadEventSO loadEventSO;
    public VoidEventSO newGameEvent;
    public VoidEventSO backToMenuEvent;

    [Header("Broadcast")]
    public VoidEventSO afterSceneLoadedEvent;
    public FadeEventSO fadeEvent;
    public SceneLoadEventSO unloadedSceneEvent;

    [Header("Scenes")]
    public GameSceneSO firstLoadScene;
    public GameSceneSO menuScene;

    private GameSceneSO currentLoadedScene;
    private GameSceneSO sceneToLoad;
    private Vector3 positionToGo;
    private bool fadeScreen;
    private bool isLoading;
    public float fadeDuration = 0.5f;

    private void Start()
    {
        // 先启用备用相机，确保从第一帧起就有相机渲染（避免首次加载 Menu 时闪烁）
        if (fallbackCamera != null)
            fallbackCamera.gameObject.SetActive(true);

        if (loadEventSO != null && menuScene != null)
        {
            loadEventSO.RaiseLoadRequestEvent(menuScene, menuPosition, true);
        }
    }

    private void OnEnable()
    {
        if (loadEventSO != null)
        {
            loadEventSO.LoadRequestEvent += OnLoadRequestEvent;
        }

        if (newGameEvent != null)
        {
            newGameEvent.OnEventRaised += NewGame;
        }

        if (backToMenuEvent != null)
        {
            backToMenuEvent.OnEventRaised += OnBackToMenuEvent;
        }

        ISaveable saveable = this;
        saveable.RegisterSaveData();
    }

    private void OnDisable()
    {
        if (loadEventSO != null)
        {
            loadEventSO.LoadRequestEvent -= OnLoadRequestEvent;
        }

        if (newGameEvent != null)
        {
            newGameEvent.OnEventRaised -= NewGame;
        }

        if (backToMenuEvent != null)
        {
            backToMenuEvent.OnEventRaised -= OnBackToMenuEvent;
        }

        ISaveable saveable = this;
        saveable.UnregisterSaveData();
    }

    private void OnBackToMenuEvent()
    {
        if (menuScene == null || loadEventSO == null)
        {
            return;
        }

        sceneToLoad = menuScene;
        loadEventSO.RaiseLoadRequestEvent(sceneToLoad, menuPosition, true);
    }

    private void NewGame()
    {
        if (firstLoadScene == null || loadEventSO == null)
        {
            return;
        }

        sceneToLoad = firstLoadScene;
        loadEventSO.RaiseLoadRequestEvent(sceneToLoad, firstPosition, true);
    }

    private void OnLoadRequestEvent(GameSceneSO locationToLoad, Vector3 posToGo, bool fadeScreen)
    {
        if (isLoading || locationToLoad == null)
        {
            return;
        }

        isLoading = true;
        sceneToLoad = locationToLoad;
        positionToGo = posToGo;
        this.fadeScreen = fadeScreen;

        // 切换场景前先启用备用相机，避免卸载旧场景后出现 "No cameras rendering"
        if (fallbackCamera != null)
            fallbackCamera.gameObject.SetActive(true);

        if (currentLoadedScene != null)
        {
            StartCoroutine(UnLoadPreviousScene());
        }
        else
        {
            LoadNewScene();
        }
    }

    private IEnumerator UnLoadPreviousScene()
    {
        if (fadeScreen && fadeEvent != null)
        {
            fadeEvent.FadeIn(fadeDuration);
        }

        yield return new WaitForSeconds(fadeDuration);

        if (unloadedSceneEvent != null)
        {
            unloadedSceneEvent.RaiseLoadRequestEvent(sceneToLoad, positionToGo, true);
        }

        if (currentLoadedScene != null)
        {
            yield return currentLoadedScene.sceneReference.UnLoadScene();
        }

        // 不在此处禁用 Player，避免相机随之关闭导致 "No cameras rendering"
        // 改为在 OnLoadCompleted 中根据新场景类型再设置 Player 激活状态

        LoadNewScene();
    }

    private void LoadNewScene()
    {
        var loadingOption = sceneToLoad.sceneReference.LoadSceneAsync(LoadSceneMode.Additive, true);
        loadingOption.Completed += OnLoadCompleted;
    }

    private void OnLoadCompleted(AsyncOperationHandle<SceneInstance> obj)
    {
        currentLoadedScene = sceneToLoad;
        ApplyRenderScaleForScene(currentLoadedScene);

        if (playerTrans != null)
        {
            playerTrans.position = positionToGo;
            bool enablePlayer = currentLoadedScene.sceneType != SceneType.Menu;
            playerTrans.gameObject.SetActive(enablePlayer);
            // 有 Player 相机时关闭备用相机，避免双相机；Menu 或无 Player 时保留备用相机
            if (fallbackCamera != null && enablePlayer)
                fallbackCamera.gameObject.SetActive(false);
        }

        if (fadeScreen && fadeEvent != null)
        {
            fadeEvent.FadeOut(fadeDuration);
        }

        isLoading = false;

        if (currentLoadedScene.sceneType == SceneType.Loaction && afterSceneLoadedEvent != null)
        {
            afterSceneLoadedEvent.RaiseEvent();
        }
    }

    private void ApplyRenderScaleForScene(GameSceneSO targetScene)
    {
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null)
        {
            return;
        }

        if (targetScene != null && targetScene.useShader)
        {
            urpAsset.renderScale = 0.3f;
        }
        else
        {
            urpAsset.renderScale = 1f;
        }
    }

    public DataDefination GetDataID()
    {
        return GetComponent<DataDefination>();
    }

    public void GetSaveData(Data data)
    {
        data.SaveGameScene(currentLoadedScene);
    }

    public void LoadSaveData(Data data)
    {
        if (playerTrans == null)
        {
            return;
        }

        var playerID = playerTrans.GetComponent<DataDefination>().ID;
        if (data.characterPosDict.ContainsKey(playerID))
        {
            positionToGo = data.characterPosDict[playerID].ToVector3();
            sceneToLoad = data.GetSavedScene();
            OnLoadRequestEvent(sceneToLoad, positionToGo, true);
        }
    }
}
