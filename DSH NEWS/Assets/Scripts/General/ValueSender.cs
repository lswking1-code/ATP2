using System.Globalization;
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
        if (GlitchEvent == null) return;
        GlitchEvent.RaiseEvent(index, delay);
    }

    public void StartGlitchingVideo(int index, float delay)
    {
        if (GlitchVideoEvent == null) return;
        GlitchVideoEvent.RaiseEvent(index, delay);
    }

    // Dialogue System Scene Event 请使用 DialogueGlitchInvoker（UnityEvent<GameObject> 无法绑定带 int/float 的方法）。

    /// <summary>
    /// 供 Dialogue System <c>Sequence</c> 里 <c>SendMessage</c> 调用（第二参为字符串）。
    /// 格式：<c>index|秒数</c>，例如 <c>2|1.5</c>。也支持 <c>index,秒数</c>（从其它脚本传入时）。
    /// <para>
    /// Sequence 里不要使用裸逗号写在一起（解析器会把参数截断）。请用竖线，或对逗号转义：<c>2\,1.5</c>。
    /// 示例：<c>SendMessage(GlitchFromSequence, 2|1.5, 你的物体名);</c>
    /// 可与 <c>Delay</c> 组合：<c>Delay(0.5); SendMessage(GlitchFromSequence, 2|1.5, 你的物体名);</c>
    /// </para>
    /// </summary>
    public void GlitchFromSequence(string payload)
    {
        if (GlitchEvent == null || !TryParseGlitchPayload(payload, out int index, out float delay))
            return;
        GlitchEvent.RaiseEvent(index, delay);
    }

    /// <summary> 同 <see cref="GlitchFromSequence"/>，触发 <see cref="GlitchVideoEvent"/>。 </summary>
    public void GlitchVideoFromSequence(string payload)
    {
        if (GlitchVideoEvent == null || !TryParseGlitchPayload(payload ?? "", out int index, out float delay))
            return;
        GlitchVideoEvent.RaiseEvent(index, delay);
    }

    /// <summary> 动画与视频各触发一次（使用同一 index 与延迟）。 </summary>
    public void GlitchBothFromSequence(string payload)
    {
        if (!TryParseGlitchPayload(payload, out int index, out float delay))
            return;
        if (GlitchEvent != null)
            GlitchEvent.RaiseEvent(index, delay);
        if (GlitchVideoEvent != null)
            GlitchVideoEvent.RaiseEvent(index, delay);
    }

    static bool TryParseGlitchPayload(string payload, out int index, out float delay)
    {
        index = 0;
        delay = 0f;
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        payload = payload.Trim();
        int sep = -1;
        for (int i = 0; i < payload.Length; i++)
        {
            char c = payload[i];
            if (c == '|' || c == ',')
            {
                sep = i;
                break;
            }
        }

        if (sep < 0)
            return int.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out index);

        string s0 = payload.Substring(0, sep).Trim();
        string s1 = payload.Substring(sep + 1).Trim();
        if (string.IsNullOrEmpty(s0))
            return false;

        if (!int.TryParse(s0, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            return false;
        if (string.IsNullOrEmpty(s1))
            return true;
        return float.TryParse(s1, NumberStyles.Float, CultureInfo.InvariantCulture, out delay);
    }

}
