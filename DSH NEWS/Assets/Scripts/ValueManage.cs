using UnityEngine;

public class ValueManage : MonoBehaviour
{
    public ValueEventSO ValueEvent;
    public float influenceValue = 0;
    public float viewership = 0;

    private void OnEnable()
    {
        ValueEvent.OnEventRaised += OnValueEventRaised;
    }

    private void OnDisable()
    {
        ValueEvent.OnEventRaised -= OnValueEventRaised;
    }

    private void OnValueEventRaised(int index, float value)
    {
        if (index == 1)
        {
            influenceValue += value;
        }
        else if (index == 2)
        {
            viewership += value;
        }
    }
}
