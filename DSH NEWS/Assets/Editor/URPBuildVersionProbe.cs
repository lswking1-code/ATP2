using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class URPBuildVersionProbe : IPreprocessBuildWithReport
{
    private const string LogPath = "debug-bea74e.log";

    public int callbackOrder => -9999;

    public void OnPreprocessBuild(BuildReport report)
    {
        var runId = "post-fix";

        var rpAssetPath = "Assets/Settings/PC_RPAsset.asset";
        var globalSettingsPath = "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset";

        var rpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rpAssetPath);
        var globalSettings = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(globalSettingsPath);

        // #region agent log
        WriteLog(
            runId,
            "H1",
            "URPBuildVersionProbe.cs:28",
            "Loaded URP assets before build",
            "{\"rpAssetPath\":\"" + Escape(rpAssetPath) + "\",\"rpAssetFound\":" + ToBool(rpAsset != null) +
            ",\"globalSettingsPath\":\"" + Escape(globalSettingsPath) + "\",\"globalSettingsFound\":" + ToBool(globalSettings != null) +
            ",\"target\":\"" + Escape(report.summary.platform.ToString()) + "\"}");
        // #endregion

        // #region agent log
        WriteLog(
            runId,
            "H2",
            "URPBuildVersionProbe.cs:42",
            "Read URP package versions",
            "{\"corePackage\":\"" + Escape(PackageVersion("com.unity.render-pipelines.core")) +
            "\",\"urpPackage\":\"" + Escape(PackageVersion("com.unity.render-pipelines.universal")) +
            "\",\"editorVersion\":\"" + Escape(Application.unityVersion) + "\"}");
        // #endregion

        // #region agent log
        WriteLog(
            runId,
            "H3",
            "URPBuildVersionProbe.cs:55",
            "Read asset serialized version fields via reflection",
            "{\"rpAssetVersion\":" + ToNumber(ReadIntField(rpAsset, "k_AssetVersion")) +
            ",\"rpAssetPreviousVersion\":" + ToNumber(ReadIntField(rpAsset, "k_AssetPreviousVersion")) +
            ",\"globalAssetVersion\":" + ToNumber(ReadIntField(globalSettings, "m_AssetVersion")) + "}");
        // #endregion

        var rpLastVersion = ReadConstInt(rpAsset, "k_LastVersion");
        var globalLastVersion = ReadConstInt(globalSettings, "k_LastVersion");

        // #region agent log
        WriteLog(
            runId,
            "H5",
            "URPBuildVersionProbe.cs:69",
            "Read expected last versions from current URP package",
            "{\"rpLastVersion\":" + ToNumber(rpLastVersion) + ",\"globalLastVersion\":" + ToNumber(globalLastVersion) + "}");
        // #endregion

        var syncChanged = SyncAssetVersionToLast(rpAsset, globalSettings, rpLastVersion, globalLastVersion);

        // #region agent log
        WriteLog(
            runId,
            "H6",
            "URPBuildVersionProbe.cs:79",
            "Applied version sync before build",
            "{\"syncChanged\":" + ToBool(syncChanged) +
            ",\"rpAssetVersionAfter\":" + ToNumber(ReadIntField(rpAsset, "k_AssetVersion")) +
            ",\"rpAssetPreviousVersionAfter\":" + ToNumber(ReadIntField(rpAsset, "k_AssetPreviousVersion")) +
            ",\"globalAssetVersionAfter\":" + ToNumber(ReadIntField(globalSettings, "m_AssetVersion")) + "}");
        // #endregion

        // #region agent log
        WriteLog(
            runId,
            "H7",
            "URPBuildVersionProbe.cs:89",
            "Checked IsAtLastVersion after sync",
            "{\"rpIsAtLastVersion\":" + ToBool(CallIsAtLastVersion(rpAsset)) +
            ",\"globalIsAtLastVersion\":" + ToBool(CallIsAtLastVersion(globalSettings)) + "}");
        // #endregion

        var qualityAsset = QualitySettings.renderPipeline;
        var graphicsAsset = GraphicsSettings.defaultRenderPipeline;

        // #region agent log
        WriteLog(
            runId,
            "H4",
            "URPBuildVersionProbe.cs:71",
            "Read active RP bindings",
            "{\"qualityRpName\":\"" + Escape(qualityAsset != null ? qualityAsset.name : "null") +
            "\",\"graphicsRpName\":\"" + Escape(graphicsAsset != null ? graphicsAsset.name : "null") +
            "\",\"qualityRpType\":\"" + Escape(qualityAsset != null ? qualityAsset.GetType().FullName : "null") +
            "\",\"graphicsRpType\":\"" + Escape(graphicsAsset != null ? graphicsAsset.GetType().FullName : "null") + "\"}");
        // #endregion
    }

    private static bool SyncAssetVersionToLast(UniversalRenderPipelineAsset rpAsset, RenderPipelineGlobalSettings globalSettings, int? rpLastVersion, int? globalLastVersion)
    {
        bool changed = false;

        if (rpAsset != null && rpLastVersion.HasValue)
        {
            var so = new SerializedObject(rpAsset);
            var v = so.FindProperty("k_AssetVersion");
            var pv = so.FindProperty("k_AssetPreviousVersion");
            if (v != null && v.intValue != rpLastVersion.Value)
            {
                v.intValue = rpLastVersion.Value;
                changed = true;
            }

            if (pv != null && pv.intValue != rpLastVersion.Value)
            {
                pv.intValue = rpLastVersion.Value;
                changed = true;
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rpAsset);
            }
        }

        if (globalSettings != null && globalLastVersion.HasValue)
        {
            var so = new SerializedObject(globalSettings);
            var v = so.FindProperty("m_AssetVersion");
            if (v != null && v.intValue != globalLastVersion.Value)
            {
                v.intValue = globalLastVersion.Value;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(globalSettings);
                changed = true;
            }
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
        }

        return changed;
    }

    private static string PackageVersion(string packageName)
    {
        var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + packageName);
        return packageInfo != null ? packageInfo.version : "not-found";
    }

    private static int? ReadIntField(object target, string fieldName)
    {
        if (target == null)
        {
            return null;
        }

        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = target.GetType();
        var field = type.GetField(fieldName, flags);
        if (field == null)
        {
            return null;
        }

        var value = field.GetValue(target);
        if (value is int intValue)
        {
            return intValue;
        }

        return null;
    }

    private static int? ReadConstInt(object target, string fieldName)
    {
        if (target == null)
        {
            return null;
        }

        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var field = target.GetType().GetField(fieldName, flags);
        if (field == null)
        {
            return null;
        }

        var value = field.GetValue(null);
        if (value is int intValue)
        {
            return intValue;
        }

        return null;
    }

    private static bool CallIsAtLastVersion(object target)
    {
        if (target == null)
        {
            return false;
        }

        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var method = target.GetType().GetMethod("IsAtLastVersion", flags);
        if (method == null)
        {
            return false;
        }

        var result = method.Invoke(target, null);
        return result is bool ok && ok;
    }

    private static void WriteLog(string runId, string hypothesisId, string location, string message, string dataJson)
    {
        try
        {
            var sb = new StringBuilder(512);
            sb.Append("{\"sessionId\":\"bea74e\"");
            sb.Append(",\"runId\":\"").Append(Escape(runId)).Append("\"");
            sb.Append(",\"hypothesisId\":\"").Append(Escape(hypothesisId)).Append("\"");
            sb.Append(",\"location\":\"").Append(Escape(location)).Append("\"");
            sb.Append(",\"message\":\"").Append(Escape(message)).Append("\"");
            sb.Append(",\"data\":").Append(dataJson);
            sb.Append(",\"timestamp\":").Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            sb.Append("}");
            var json = sb.ToString();
            File.AppendAllText(LogPath, json + Environment.NewLine);
        }
        catch
        {
            // ignore logging failures in probe
        }
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string ToBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string ToNumber(int? value)
    {
        return value.HasValue ? value.Value.ToString() : "null";
    }
}
