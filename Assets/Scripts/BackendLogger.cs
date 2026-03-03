using System;
using UnityEngine;

public static class BackendLogger
{
    private const string Prefix = "[Backend]";

    public static void Info(string sourceFile, string eventName, string details = null)
    {
        Debug.Log(Format("INFO", sourceFile, eventName, details));
    }

    public static void Warn(string sourceFile, string eventName, string details = null)
    {
        Debug.LogWarning(Format("WARN", sourceFile, eventName, details));
    }

    public static void Error(string sourceFile, string eventName, string details = null)
    {
        Debug.LogError(Format("ERROR", sourceFile, eventName, details));
    }

    public static void Error(string sourceFile, string eventName, Exception exception, string details = null)
    {
        string exDetails = exception == null ? null : $"exception={exception.GetType().Name}, message={exception.Message}";
        string merged = Merge(details, exDetails);
        Debug.LogError(Format("ERROR", sourceFile, eventName, merged));
    }

    public static void Verbose(bool enabled, string sourceFile, string eventName, string details = null)
    {
        if (!enabled)
            return;

        Debug.Log(Format("DEBUG", sourceFile, eventName, details));
    }

    private static string Format(string level, string sourceFile, string eventName, string details)
    {
        string baseMessage = $"{Prefix}[{level}][{sourceFile}] {eventName}";
        return string.IsNullOrWhiteSpace(details) ? baseMessage : $"{baseMessage} | {details}";
    }

    private static string Merge(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
            return right;
        if (string.IsNullOrWhiteSpace(right))
            return left;
        return $"{left}, {right}";
    }
}