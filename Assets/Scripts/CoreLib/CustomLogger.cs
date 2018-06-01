using UnityEngine;

public class CustomLogger : Logger
{
    public CustomLogger(ILogHandler logHandler) : base(logHandler)
    {
    }

    public void LogError(string message)
    {
        LogError("Error", message);
    }
}