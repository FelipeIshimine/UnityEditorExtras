using System;

[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ShowInInspectorAttribute : Attribute
{
    public string Label;

    public ShowInInspectorAttribute(string label = null)
    {
        Label = label;
    }
}