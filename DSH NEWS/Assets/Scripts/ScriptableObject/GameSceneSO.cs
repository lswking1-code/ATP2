using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Game Scene/GameSceneSO")]
public class GameSceneSO : ScriptableObject
{
    public SceneType sceneType;
    public AssetReference sceneReference;
    public bool useShader;
    public bool useFullScreenRetro;
    [Tooltip("为 True 时启用 PC_Renderer 的 Blit（VHS 后处理）；为 False 时关闭")]
    public bool useVHSEffect;
}
