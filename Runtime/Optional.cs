namespace UnityEditorExtras.Runtime
{
    using System;
    using UnityEngine;

    /// <summary>
    /// A serializable wrapper for fields that are truly optional in the Inspector.
    /// Includes an implicit boolean cast to allow clean 'if (myOptionalField)' checks,
    /// and an implicit cast to T for easy access.
    /// </summary>
    [Serializable]
    public struct Optional<T>
    {
        [SerializeField] private bool _enabled;
        [SerializeField] private T _value;

        public bool Enabled => _enabled;
        public T Value => _enabled ? _value : default;

        // Implicit cast to bool for clean checks: if (myOptionalField) { ... }
        public static implicit operator bool(Optional<T> optional) => optional._enabled && optional._value != null;
        
        // Implicit cast to T for easy access: T val = myOptionalField;
        public static implicit operator T(Optional<T> optional) => optional.Value;

        public Optional(T value, bool enabled = true)
        {
            _value = value;
            _enabled = enabled;
        }
    }
}
