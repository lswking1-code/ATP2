using UnityEngine;

public class ValueSender : MonoBehaviour
{



    public ValueEventSO valueEvent;

    public GlitchEventSO GlitchEvent;
    public GlitchEventSO GlitchVideoEvent;



    [System.Serializable]
    public class ValueData
    {
        public int index;
        public float amount;

    }

    

    public void SendValueData(ValueData data)
    {
        SendValue(data.index, data.amount);
    }

    

    public void SendValue(int index, float amount)
    {
        valueEvent.RaiseEvent(index,amount);
        Debug.Log("Send Value!");


    }

    public void SendNormalValue() => SendValue(2, 1.0f);

    public void StartGlitching(int index, float delay)
    {
        GlitchEvent.RaiseEvent(index, delay);
    }

    public void StartGlitchingVideo(int index, float delay)
    {
        GlitchVideoEvent.RaiseEvent(index, delay);
    }



}
