namespace UnityEditorExtras.Runtime
{
    using System;

    /// <summary>
    /// A wrapper for dependencies assigned at runtime.
    /// Ensures you never access a null dependency without a descriptive error.
    /// </summary>
    public struct Required<T> where T : class
    {
        private T _value;
        
        public T Value => _value ?? throw new InvalidOperationException($"{typeof(T).Name} is required but has not been initialized.");
        
        public Required(T value) => _value = value;
        
        public static implicit operator T(Required<T> req) => req.Value;
        
        public void Set(T value) => _value = value;
        
        public bool IsInitialized => _value != null;
    }
}
