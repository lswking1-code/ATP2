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
    [Header("圖片顯示設定")]
    [Min(0.01f)]
    public float unifiedImageHeight = 1.0f;

    [Header("可擴充內容（用 id 對應圖文）")]
    public List<OverlayContent> contents = new List<OverlayContent>();

    [Header("向後相容（舊欄位，可逐步移除）")]
    public Sprite earthquake;
    public Sprite fire;
    public Sprite news;

    private readonly Dictionary<string, OverlayContent> _contentMap = new Dictionary<string, OverlayContent>();
    private Vector3 _baseImageScale = Vector3.one;
    private bool _hasCachedBaseScale;

    private void Awake()
    {
        CacheBaseImageScale();
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

    private void CacheBaseImageScale()
    {
        if (tvImage == null) return;
        _baseImageScale = tvImage.transform.localScale;
        _hasCachedBaseScale = true;
    }

    private void ApplyUnifiedImageHeight(Sprite sprite)
    {
        if (tvImage == null || sprite == null) return;
        if (!_hasCachedBaseScale) CacheBaseImageScale();

        float spriteHeight = sprite.bounds.size.y;
        if (spriteHeight <= 0f) return;

        float scaleMultiplier = unifiedImageHeight / spriteHeight;
        tvImage.transform.localScale = _baseImageScale * scaleMultiplier;
    }

    private void SetImageSprite(Sprite sprite)
    {
        if (tvImage == null) return;
        tvImage.enabled = sprite != null;
        tvImage.sprite = sprite;

        if (sprite != null)
        {
            ApplyUnifiedImageHeight(sprite);
        }
        else if (_hasCachedBaseScale)
        {
            tvImage.transform.localScale = _baseImageScale;
        }
    }

    public void ShowContent(string id)
    {
        if (!TryGetContent(id, out OverlayContent content))
        {
            Debug.LogWarning($"[TVOverlay] 找不到內容 id: {id}", this);
            return;
        }

        SetImageSprite(content.sprite);

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

        SetImageSprite(content.sprite);
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
        SetImageSprite(null);
        if (subtitle != null)
        {
            subtitle.text = string.Empty;
        }
    }
}