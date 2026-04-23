using UnityEngine;

public class NextScene : MonoBehaviour
{
    public SceneLoadEventSO loadEventSO;
    public GameSceneSO sceneToGo;
    [Header("Day Branch")]
    public bool enableDayBasedBranch = false;
    [Min(0)] public int endingStartDay = 0;
    public GameSceneSO endingSceneToGo;
    public ValueManage valueManageOverride;
    public Vector3 positionToGo;
    public Vector3 rotationToGo;
    public VoidEventSO SwitchScanLineEvent;
    private void Awake()
    {
        SwitchScanLineEvent.RaiseEvent();
    }
    public void OnNextScene()
    {
        GameSceneSO targetScene = ResolveTargetScene();
        if (loadEventSO == null || targetScene == null)
        {
            return;
        }
        SwitchScanLineEvent.RaiseEvent();
        loadEventSO.RaiseLoadRequestEvent(targetScene, positionToGo, rotationToGo, true);
    }

    private GameSceneSO ResolveTargetScene()
    {
        if (!enableDayBasedBranch || endingSceneToGo == null)
        {
            return sceneToGo;
        }

        ValueManage valueManage = valueManageOverride != null
            ? valueManageOverride
            : FindFirstObjectByType<ValueManage>();

        if (valueManage == null)
        {
            return sceneToGo;
        }

        return valueManage.day >= endingStartDay ? endingSceneToGo : sceneToGo;
    }
}
