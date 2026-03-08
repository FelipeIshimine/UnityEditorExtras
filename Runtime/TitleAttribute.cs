using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class InspectorTitleAttribute : PropertyAttribute
{
    public readonly int FontSize;
    public readonly TitleAlign Alignment;

    public InspectorTitleAttribute(
        int fontSize = 14,
        TitleAlign alignment = TitleAlign.Left)
    {
        FontSize = fontSize;
        Alignment = alignment;
    }
}

public enum TitleAlign
{
    Left,
    Center,
    Right
}