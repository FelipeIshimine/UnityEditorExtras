using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public sealed class SceneSelectionAttribute : PropertyAttribute
{
    public SceneSelectionAttribute()
    {
    }
}
