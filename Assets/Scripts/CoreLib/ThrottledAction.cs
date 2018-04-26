using System;
using System.Diagnostics;

public class ThrottledAction
{
    public ThrottledAction(Action action, float intervalInSeconds)
    {
        this.action = action;
        this.intervalInSeconds = intervalInSeconds;
        lastCallTimestamp = 0;
    }
    public bool TryToCall()
    {
        var currentTimestamp = Stopwatch.GetTimestamp();
        var secondsSinceLastCall = (float)(currentTimestamp - lastCallTimestamp) / Stopwatch.Frequency;
        if (secondsSinceLastCall >= intervalInSeconds)
        {
            action();
            lastCallTimestamp = currentTimestamp;
            return true;
        }
        else
        {
            return false;
        }
    }

    private Action action;
    private float intervalInSeconds;
    private long lastCallTimestamp;
}