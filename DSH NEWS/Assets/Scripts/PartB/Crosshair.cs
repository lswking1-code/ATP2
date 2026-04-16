using UnityEngine;

public class Crosshair : MonoBehaviour
{
    [Header("Outlook")]
    [SerializeField] private Color color = Color.white;
    [SerializeField, Tooltip("׼�Ǵ�С�����أ�")] private float size = 4f;
    [SerializeField, Tooltip("�ɽ���Ŀ�������׼ʱ�ĸ�����Ʉ1�7")] private Color highlightColor = Color.green;

    [Header("Setting")]
    [SerializeField, Tooltip("�����δ����ʱ�Ƿ�Ҳ��ʾ׼�ￄ1�7")] private bool showWhenCursorUnlocked = false;
    [SerializeField, Tooltip("�Ƿ��ڿ�ʼʱ����׼��")] private bool enabledAtStart = true;

    private Texture2D tex;
    private bool isEnabled;
    private bool isHighlighted;

    private void Awake()
    {
        // Create 1x1 white texture for crosshair drawing.
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
        if (!showWhenCursorUnlocked && Cursor.lockState != CursorLockMode.Locked) return; // Hide when unlocked unless explicitly allowed.

        var oldColor = GUI.color;
        GUI.color = isHighlighted ? highlightColor : color;

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        // Draw center block crosshair.
        GUI.DrawTexture(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), tex);

        GUI.color = oldColor;
    }

    public void SetEnabled(bool on) => isEnabled = on;
    public bool IsEnabled() => isEnabled;

    public void SetHighlighted(bool on) => isHighlighted = on;
    public bool IsHighlighted() => isHighlighted;
}