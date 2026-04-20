using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;

public class TVOverlay : MonoBehaviour
{
    [Serializable]
    public class OverlayContent
    {
        public string id;
        public Sprite sprite;
        [TextArea]
        public string subtitle;
    }

    public SpriteRenderer tvImage;
    public TextMeshPro subtitle;

    [Header("可擴充內容（用 id 對應圖文）")]
    public List<OverlayContent> contents = new List<OverlayContent>();

    [Header("向後相容（舊欄位，可逐步移除）")]
    public Sprite earthquake;
    public Sprite fire;
    public Sprite news;

    private readonly Dictionary<string, OverlayContent> _contentMap = new Dictionary<string, OverlayContent>();

    private void Awake()
    {
        BuildContentMap();
    }

    private void OnValidate()
    {
        if (contents == null)
        {
            contents = new List<OverlayContent>();
        }

        // 把舊欄位同步進新清單，避免既有場景立刻失效。
        EnsureLegacyEntry("earthquake", earthquake);
        EnsureLegacyEntry("fire", fire);
        EnsureLegacyEntry("news", news);
    }

    private void EnsureLegacyEntry(string id, Sprite sprite)
    {
        if (sprite == null) return;

        for (int i = 0; i < contents.Count; i++)
        {
            if (string.Equals(contents[i].id, id, StringComparison.OrdinalIgnoreCase))
            {
                if (contents[i].sprite == null) contents[i].sprite = sprite;
                return;
            }
        }

        contents.Add(new OverlayContent
        {
            id = id,
            sprite = sprite,
            subtitle = string.Empty
        });
    }

    private void BuildContentMap()
    {
        _contentMap.Clear();
        if (contents == null) return;

        for (int i = 0; i < contents.Count; i++)
        {
            OverlayContent content = contents[i];
            if (content == null || string.IsNullOrWhiteSpace(content.id))
            {
                continue;
            }

            string key = content.id.Trim();
            if (_contentMap.ContainsKey(key))
            {
                Debug.LogWarning($"[TVOverlay] 重複 id：{key}，後者已忽略。", this);
                continue;
            }

            _contentMap.Add(key, content);
        }
    }

    private bool TryGetContent(string id, out OverlayContent content)
    {
        content = null;
        if (string.IsNullOrWhiteSpace(id)) return false;

        if (_contentMap.Count == 0)
        {
            BuildContentMap();
        }

        return _contentMap.TryGetValue(id.Trim(), out content);
    }

    public void ShowContent(string id)
    {
        if (!TryGetContent(id, out OverlayContent content))
        {
            Debug.LogWarning($"[TVOverlay] 找不到內容 id: {id}", this);
            return;
        }

        tvImage.enabled = content.sprite != null;
        tvImage.sprite = content.sprite;

        if (subtitle != null)
        {
            subtitle.text = content.subtitle ?? string.Empty;
        }
    }

    public void ShowImage(string id)
    {
        if (!TryGetContent(id, out OverlayContent content))
        {
            Debug.LogWarning($"[TVOverlay] 找不到圖片 id: {id}", this);
            return;
        }

        tvImage.enabled = content.sprite != null;
        tvImage.sprite = content.sprite;
    }

    public void Subtitle(string text)
    {
        if (subtitle == null) return;
        subtitle.text = text;
    }

    public void SubtitleById(string id)
    {
        if (subtitle == null) return;

        if (!TryGetContent(id, out OverlayContent content))
        {
            Debug.LogWarning($"[TVOverlay] 找不到字幕 id: {id}", this);
            return;
        }

        subtitle.text = content.subtitle ?? string.Empty;
    }

    public void HideImage()
    {
        HideBroadcast();
    }

    // 一次關閉圖片與新聞字幕
    public void HideBroadcast()
    {
        tvImage.enabled = false;
        tvImage.sprite = null;
        if (subtitle != null)
        {
            subtitle.text = string.Empty;
        }
    }
}