using System;
using UnityEngine;

[System.Serializable]
public class Timer
{
    public event Action OnCompletion;
    public event Action OnRestart;
        
    [SerializeField] private float duration = 1;
    [SerializeField] private float remaining;
    [SerializeField] private bool restartOnCompletion;

    public Timer()
    {
    }
        
    public Timer(float duration)
    {
        this.duration = duration;
    }

    public Timer(float duration, bool restartOnCompletion)
    {
        this.remaining = this.duration = duration;
        this.restartOnCompletion = restartOnCompletion;
    }

    public float Progression => 1f - remaining / duration;

    public void SetDuration(float newDuration)
    {
        duration = newDuration;
        remaining = Mathf.Min(duration, remaining);
    }

    public void Restart()
    {
        remaining = duration;
        OnRestart?.Invoke();
    }
        
    public bool Advance(float delta)
    {
        if (remaining > 0)
        {
            remaining -= delta;
        }

        if (remaining <= 0)
        {
            if (restartOnCompletion)
            {
                remaining = duration;
            }
            OnCompletion?.Invoke();
            return true;
        }
        return false;
    }
}