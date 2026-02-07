using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour, ISaveable
{
    public Transform playerTrans;
    public Vector3 firstPosition;
    public Vector3 menuPosition;

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

        if (playerTrans != null)
        {
            playerTrans.gameObject.SetActive(false);
        }

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

        if (playerTrans != null)
        {
            playerTrans.position = positionToGo;
        }

        if (playerTrans != null)
        {
            playerTrans.gameObject.SetActive(true);
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
