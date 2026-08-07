using System;
using System.IO;
using UnityEngine;

public static class AgentRuntimeLogger
{
    private const string SessionId = "9571b5";
    // Debug mode requires writing to this exact workspace log file.
    private const string WorkspaceLogPath = @"D:\Github\ATP2\DSH NEWS\debug-9571b5.log";
    private static readonly string BuildLocalLogPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "debug-9571b5.log"));

    public static void Log(string runId, string hypothesisId, string location, string message, string dataJson)
    {
        try
        {
            string line =
                "{\"sessionId\":\"" + Escape(SessionId) +
                "\",\"runId\":\"" + Escape(runId) +
                "\",\"hypothesisId\":\"" + Escape(hypothesisId) +
                "\",\"location\":\"" + Escape(location) +
                "\",\"message\":\"" + Escape(message) +
                "\",\"data\":" + (string.IsNullOrEmpty(dataJson) ? "{}" : dataJson) +
                ",\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() +
                "}";

            TryAppend(WorkspaceLogPath, line);
            if (!string.Equals(BuildLocalLogPath, WorkspaceLogPath, StringComparison.OrdinalIgnoreCase))
            {
                TryAppend(BuildLocalLogPath, line);
            }
        }
        catch
        {
            // Keep gameplay stable if logging fails.
        }
    }

    private static void TryAppend(string path, string line)
    {
        try
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
        catch
        {
            // Ignore path-specific write errors and keep trying others.
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
}
