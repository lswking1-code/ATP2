using UnityEngine;

public class Crosshair : MonoBehaviour
{
    [Header("Outlook")]
    [SerializeField] private Color color = Color.white;
    [SerializeField, Tooltip("点的大小（像素）")] private float size = 4f;

    [Header("Setting")]
    [SerializeField, Tooltip("是否在解锁鼠标时也显示")] private bool showWhenCursorUnlocked = false;
    [SerializeField, Tooltip("是否启用准星")] private bool enabledAtStart = true;

    private Texture2D tex;
    private bool isEnabled;

    private void Awake()
    {
        // 创建 1x1 白色像素纹理用于绘制
        tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        isEnabled = enabledAtStart;
    }

    private void OnDestroy()
    {
        if (tex != null)
            Destroy(tex);
    }

    private void OnGUI()
    {
        if (!isEnabled) return;
        if (!showWhenCursorUnlocked && Cursor.lockState != CursorLockMode.Locked) return;

        var oldColor = GUI.color;
        GUI.color = color;

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        // 单点准星（正方形像素块）
        GUI.DrawTexture(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), tex);

        GUI.color = oldColor;
    }

    // 运行时可通过脚本启/关准星
    public void SetEnabled(bool on) => isEnabled = on;
    public bool IsEnabled() => isEnabled;
}