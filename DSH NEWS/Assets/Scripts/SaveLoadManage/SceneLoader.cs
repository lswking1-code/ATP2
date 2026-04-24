using System.Collections;
using System.Reflection;
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

    [Header("Environment Lighting")]
    [Tooltip("进入该场景时覆盖 Ambient Color（例如 Test1）")]
    public GameSceneSO ambientOverrideScene;
    public Color ambientOverrideColor = Color.gray;
    [Tooltip("进入该场景时覆盖 Skybox 材质")]
    public Material ambientOverrideSkybox;

    private GameSceneSO currentLoadedScene;
    private GameSceneSO sceneToLoad;
    private Vector3 positionToGo;
    private Vector3 rotationToGo;
    private bool fadeScreen;
    private bool isLoading;
    public float fadeDuration = 0.5f;
    private Color cachedAmbientColor;
    private Material cachedSkybox;
    private bool hasCachedAmbientColor;
    private bool hasCachedSkybox;
    private bool ambientOverrideApplied;

    private void Start()
    {
        // 先启用备用相机，确保从第一帧起就有相机渲染（避免首次加载 Menu 时闪烁）
        if (fallbackCamera != null)
            fallbackCamera.gameObject.SetActive(true);

        if (loadEventSO != null && menuScene != null)
        {
            loadEventSO.RaiseLoadRequestEvent(menuScene, menuPosition, Vector3.zero, true);
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
        loadEventSO.RaiseLoadRequestEvent(sceneToLoad, menuPosition, Vector3.zero, true);
    }

    private void NewGame()
    {
        if (firstLoadScene == null || loadEventSO == null)
        {
            return;
        }

        sceneToLoad = firstLoadScene;
        loadEventSO.RaiseLoadRequestEvent(sceneToLoad, firstPosition, Vector3.zero, true);
    }

    private void OnLoadRequestEvent(GameSceneSO locationToLoad, Vector3 posToGo, Vector3 rotToGo, bool fadeScreen)
    {
        if (isLoading || locationToLoad == null)
        {
            return;
        }

        isLoading = true;
        sceneToLoad = locationToLoad;
        positionToGo = posToGo;
        rotationToGo = rotToGo;
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
            unloadedSceneEvent.RaiseLoadRequestEvent(sceneToLoad, positionToGo, rotationToGo, true);
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
        ApplyAmbientColorForScene(currentLoadedScene);
        ApplyRenderScaleForScene(currentLoadedScene);
        ApplyFullScreenRetroForScene(currentLoadedScene);
        ApplyVHSEffectForScene(currentLoadedScene);

        if (playerTrans != null)
        {
            playerTrans.position = positionToGo;
            playerTrans.rotation = Quaternion.Euler(rotationToGo);
            bool enablePlayer = currentLoadedScene.sceneType != SceneType.Menu;
            playerTrans.gameObject.SetActive(enablePlayer);
            // 保持备用相机始终开启，以确保始终有相机为 Display1 渲染
            if (fallbackCamera != null)
                fallbackCamera.gameObject.SetActive(true);
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

    private void ApplyAmbientColorForScene(GameSceneSO targetScene)
    {
        bool shouldOverrideAmbient = targetScene != null && ambientOverrideScene != null && targetScene == ambientOverrideScene;
        if (shouldOverrideAmbient)
        {
            if (!ambientOverrideApplied)
            {
                cachedAmbientColor = RenderSettings.ambientLight;
                hasCachedAmbientColor = true;
                cachedSkybox = RenderSettings.skybox;
                hasCachedSkybox = true;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientOverrideColor;
            RenderSettings.ambientSkyColor = ambientOverrideColor;
            RenderSettings.ambientEquatorColor = ambientOverrideColor;
            RenderSettings.ambientGroundColor = ambientOverrideColor;
            if (ambientOverrideSkybox != null)
            {
                RenderSettings.skybox = ambientOverrideSkybox;
            }
            DynamicGI.UpdateEnvironment();
            ambientOverrideApplied = true;
            return;
        }

        if (ambientOverrideApplied && hasCachedAmbientColor)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = cachedAmbientColor;
            RenderSettings.ambientSkyColor = cachedAmbientColor;
            RenderSettings.ambientEquatorColor = cachedAmbientColor;
            RenderSettings.ambientGroundColor = cachedAmbientColor;
            if (hasCachedSkybox)
            {
                RenderSettings.skybox = cachedSkybox;
            }
            DynamicGI.UpdateEnvironment();
        }

        ambientOverrideApplied = false;
        hasCachedAmbientColor = false;
        hasCachedSkybox = false;
    }

    private void ApplyFullScreenRetroForScene(GameSceneSO targetScene)
    {
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null)
        {
            return;
        }

        // 兼容不同 URP 版本：优先用属性 scriptableRendererData，失败则退回到 m_RendererDataList + m_DefaultRendererIndex
        ScriptableRendererData rendererData = null;

        var urpType = urpAsset.GetType();
        var srProp = urpType.GetProperty("scriptableRendererData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (srProp != null)
        {
            rendererData = srProp.GetValue(urpAsset) as ScriptableRendererData;
        }

        if (rendererData == null)
        {
            var listField = urpType.GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
            var indexField = urpType.GetField("m_DefaultRendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);

            if (listField != null && indexField != null)
            {
                var list = listField.GetValue(urpAsset) as ScriptableRendererData[];
                if (list != null && list.Length > 0)
                {
                    int defaultIndex = (int)indexField.GetValue(urpAsset);
                    if (defaultIndex >= 0 && defaultIndex < list.Length)
                    {
                        rendererData = list[defaultIndex];
                    }
                }
            }
        }

        if (rendererData == null || rendererData.rendererFeatures == null)
        {
            return;
        }

        foreach (var feature in rendererData.rendererFeatures)
        {
            if (feature != null && feature.name == "FullScreenPassRendererFeatureRetroDither")
            {
                bool enable = targetScene != null && targetScene.useFullScreenRetro;
                feature.SetActive(enable);
                break;
            }
        }
    }

    private void ApplyVHSEffectForScene(GameSceneSO targetScene)
    {
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null)
        {
            return;
        }

        ScriptableRendererData rendererData = null;
        var urpType = urpAsset.GetType();
        var srProp = urpType.GetProperty("scriptableRendererData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (srProp != null)
        {
            rendererData = srProp.GetValue(urpAsset) as ScriptableRendererData;
        }

        if (rendererData == null)
        {
            var listField = urpType.GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
            var indexField = urpType.GetField("m_DefaultRendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);

            if (listField != null && indexField != null)
            {
                var list = listField.GetValue(urpAsset) as ScriptableRendererData[];
                if (list != null && list.Length > 0)
                {
                    int defaultIndex = (int)indexField.GetValue(urpAsset);
                    if (defaultIndex >= 0 && defaultIndex < list.Length)
                    {
                        rendererData = list[defaultIndex];
                    }
                }
            }
        }

        if (rendererData == null || rendererData.rendererFeatures == null)
        {
            return;
        }

        foreach (var feature in rendererData.rendererFeatures)
        {
            if (feature != null && feature.name == "Blit")
            {
                bool enable = targetScene != null && targetScene.useVHSEffect;
                feature.SetActive(enable);
                break;
            }
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
            OnLoadRequestEvent(sceneToLoad, positionToGo, playerTrans.rotation.eulerAngles, true);
        }
    }
}
