using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class DataManager : MonoBehaviour
{
    public static DataManager instance;

    [Header("Events")]
    public VoidEventSO saveDataEvent;
    public VoidEventSO loadDataEvent;

    private readonly List<ISaveable> saveableList = new List<ISaveable>();
    private Data saveData;
    private string jsonFolder;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        saveData = new Data();
        jsonFolder = Application.persistentDataPath + "/SAVE DATA/";
        ReadSavedData();
    }

    private void OnEnable()
    {
        if (saveDataEvent != null)
        {
            saveDataEvent.OnEventRaised += Save;
        }

        if (loadDataEvent != null)
        {
            loadDataEvent.OnEventRaised += Load;
        }
    }

    private void OnDisable()
    {
        if (saveDataEvent != null)
        {
            saveDataEvent.OnEventRaised -= Save;
        }

        if (loadDataEvent != null)
        {
            loadDataEvent.OnEventRaised -= Load;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
        {
            Load();
        }
    }

    public void RegisterSaveData(ISaveable saveable)
    {
        if (!saveableList.Contains(saveable))
        {
            saveableList.Add(saveable);
        }
    }

    public void UnRegisterSaveData(ISaveable saveable)
    {
        saveableList.Remove(saveable);
    }

    public void Save()
    {
        foreach (var saveable in saveableList)
        {
            saveable.GetSaveData(saveData);
        }

        var resultPath = jsonFolder + "data.sav";
        var jsonData = JsonConvert.SerializeObject(saveData);

        if (!File.Exists(resultPath))
        {
            Directory.CreateDirectory(jsonFolder);
        }

        File.WriteAllText(resultPath, jsonData);
    }

    public void Load()
    {
        foreach (var saveable in saveableList)
        {
            saveable.LoadSaveData(saveData);
        }
    }

    private void ReadSavedData()
    {
        var resultPath = jsonFolder + "data.sav";

        if (File.Exists(resultPath))
        {
            var stringData = File.ReadAllText(resultPath);
            var jsonData = JsonConvert.DeserializeObject<Data>(stringData);
            saveData = jsonData;
        }
    }
}
