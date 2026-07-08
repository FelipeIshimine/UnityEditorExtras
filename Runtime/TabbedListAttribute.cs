using System;
using UnityEngine;

namespace UnityEditorExtras.Runtime
{
    /// <summary>
    /// Draws a list/array field as a tab strip: only the selected element's body is shown at a
    /// time, keeping long lists readable. Targets the collection itself (not its items) via
    /// <see cref="PropertyAttribute"/>'s applyToCollection flag.
    ///
    /// Usage:
    ///   [TabbedList] public List&lt;UpgradeTier&gt; tiers = new();
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TabbedListAttribute : PropertyAttribute
    {
        public TabbedListAttribute() : base(applyToCollection: true) { }
    }
}
