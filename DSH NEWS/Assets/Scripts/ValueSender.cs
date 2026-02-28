using UnityEngine;

public class ValueSender : MonoBehaviour
{



    public ValueEventSO valueEvent;

    public GlitchEventSO GlitchEvent;



    public void SendValue(int index, float amount)
    {
        valueEvent.RaiseEvent(index,amount);
        Debug.Log("Send Value!");


    }

    public void SendGlitch(int index)
    {

        GlitchEvent.RaiseEvent(index);
    }


}
