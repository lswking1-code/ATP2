using System.Collections.Generic;
using UnityEngine;

public class Data
{
    public string sceneToSave;

    public Dictionary<string, SerializeVector3> characterPosDict = new Dictionary<string, SerializeVector3>();
    public Dictionary<string, float> floatSavedData = new Dictionary<string, float>();
    public Dictionary<bool, bool> boolSavedData = new Dictionary<bool, bool>();

    public void SaveGameScene(GameSceneSO savedScene)
    {
        sceneToSave = JsonUtility.ToJson(savedScene);
        Debug.Log(sceneToSave);
    }

    public GameSceneSO GetSavedScene()
    {
        var newScene = ScriptableObject.CreateInstance<GameSceneSO>();
        JsonUtility.FromJsonOverwrite(sceneToSave, newScene);
        return newScene;
    }
}

public class SerializeVector3
{
    public float x;
    public float y;
    public float z;

    public SerializeVector3(Vector3 pos)
    {
        x = pos.x;
        y = pos.y;
        z = pos.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}
