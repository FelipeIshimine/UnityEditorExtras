using System;

/// <summary>
/// Mark any MonoBehaviour, Component, or ScriptableObject with this attribute
/// to expose it in the Quick Access editor window.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class QuickAccessAttribute : Attribute
{
    /// <summary>Display label shown in the Quick Access dropdown. Defaults to class name.</summary>
    public string Label { get; }

    public QuickAccessAttribute(string label = null)
    {
        Label = label;
    }
}