using UnityEngine;

public class ValueSender : MonoBehaviour
{



    public ValueEventSO valueEvent;

    public GlitchEventSO GlitchEvent;



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





}
