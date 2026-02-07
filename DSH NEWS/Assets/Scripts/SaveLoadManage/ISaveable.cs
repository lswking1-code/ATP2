public interface ISaveable
{
    DataDefination GetDataID();
    void RegisterSaveData()
    {
        if (DataManager.instance == null)
        {
            return;
        }

        DataManager.instance.RegisterSaveData(this);
    }

    void UnregisterSaveData()
    {
        if (DataManager.instance == null)
        {
            return;
        }

        DataManager.instance.UnRegisterSaveData(this);
    }

    void GetSaveData(Data data);
    void LoadSaveData(Data data);
}
