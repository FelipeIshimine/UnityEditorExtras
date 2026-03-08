using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class SpritePreviewAttribute : PropertyAttribute
{
    public readonly float Size;
    public readonly bool ShowField;
    public readonly SpritePreviewAlignment Alignment;

    public SpritePreviewAttribute(
        float size = 120,
        bool showField = true,
        SpritePreviewAlignment alignment = SpritePreviewAlignment.Center)
    {
        Size = size;
        ShowField = showField;
        Alignment = alignment;
    }
}

public enum SpritePreviewAlignment
{
    Left,
    Right,
    Center
}