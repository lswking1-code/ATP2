using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ValueChange
{
    public int ValueIndex;
    public float ValueAmount;
}

[System.Serializable]
public class ManuscriptEntry
{
    public Manuscript Manuscript;
    public bool HasGlitch;
    public UnityEngine.TextAsset TextFile;
    public UnityEngine.TextAsset GlitchTextFile;
    public string Id = "";
    public int ScriptIndex;
    /// <summary> 点击时传递的多个数值变更 </summary>
    public List<ValueChange> Values = new List<ValueChange>();
    // public int ValueIndex;
    // public float ValueAmount;
}
