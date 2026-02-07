using UnityEngine;

public class Savepoint : MonoBehaviour, IInteractable
{
    [Header("Events")]
    public VoidEventSO saveDataEvent;

    [Header("Visual")]
    public SpriteRenderer spriteRenderer;
    public Sprite darkSprite;
    public Sprite lightSprite;
    public bool isDone;

    private void OnEnable()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = isDone ? lightSprite : darkSprite;
        }
    }

    public void OnInteract()
    {
        if (isDone)
        {
            return;
        }

        isDone = true;
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = lightSprite;
        }

        if (saveDataEvent != null)
        {
            saveDataEvent.RaiseEvent();
        }

        gameObject.tag = "Untagged";
    }
}
