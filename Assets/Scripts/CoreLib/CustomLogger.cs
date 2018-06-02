using UnityEngine;

public class CustomLogger : Logger
{
    public CustomLogger(ILogHandler logHandler) : base(logHandler)
    {
    }

    public void LogWarning(string message)
    {
        LogWarning("Warning", message);
    }
    public void LogError(string message)
    {
        LogError("Error", message);
    }
}