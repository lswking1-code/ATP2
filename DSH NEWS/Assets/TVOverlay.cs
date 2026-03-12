using UnityEngine;
using TMPro;

public class TVOverlay : MonoBehaviour
{
    public SpriteRenderer tvImage;
    public TextMeshPro subtitle;

    public Sprite earthquake;
    public Sprite fire;
    public Sprite news;

    public void ShowImage(string id)
    {
        tvImage.enabled = true;

        if (id == "earthquake") tvImage.sprite = earthquake;
        if (id == "fire") tvImage.sprite = fire;
        if (id == "news") tvImage.sprite = news;
    }

    public void Subtitle(string text)
    {
        subtitle.text = text;
    }

    public void HideImage()
    {
        tvImage.enabled = false;
        subtitle.text = "";
    }
}