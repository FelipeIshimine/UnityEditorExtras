namespace UnityEditorExtras.Runtime
{
    using System;
    using UnityEngine;

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class InlineAttribute : PropertyAttribute
    {
        public readonly bool ShowLabel;
        public readonly int IndentPixels;

        public InlineAttribute(bool showLabel = true, int indentPixels = 6)
        {
            if (indentPixels < 0)
                throw new ArgumentOutOfRangeException(nameof(indentPixels), indentPixels, "Inline indent cannot be negative.");

            ShowLabel = showLabel;
            IndentPixels = indentPixels;
        }
    }
}
