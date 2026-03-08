using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A serializable collection that displays in the inspector with duplicate
/// entries collapsed into a single row with an editable count badge.
///
/// Wraps a T[] directly so Unity serializes it as a predictable child
/// property named "items" — no backing field hunting needed.
///
/// Usage:
///   public CompactList&lt;CardDefinition&gt; startCards = new();
/// </summary>
[Serializable]
public class CompactList<T> : IList<T>, IReadOnlyList<T> where T : UnityEngine.Object
{
    [SerializeField] public T[] items = Array.Empty<T>();

    // IList<T> implementation so existing code using list operations still works

    public T this[int index]
    {
        get => items[index];
        set => items[index] = value;
    }

    public int Count => items.Length;
    public bool IsReadOnly => false;

    public void Add(T item)
    {
        Array.Resize(ref items, items.Length + 1);
        items[^1] = item;
    }

    public void Clear() => items = Array.Empty<T>();

    public bool Contains(T item) => Array.IndexOf(items, item) >= 0;

    public void CopyTo(T[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);

    public int IndexOf(T item) => Array.IndexOf(items, item);

    public void Insert(int index, T item)
    {
        var list = new List<T>(items);
        list.Insert(index, item);
        items = list.ToArray();
    }

    public bool Remove(T item)
    {
        var idx = Array.IndexOf(items, item);
        if (idx < 0) return false;
        RemoveAt(idx);
        return true;
    }

    public void RemoveAt(int index)
    {
        var list = new List<T>(items);
        list.RemoveAt(index);
        items = list.ToArray();
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();
}