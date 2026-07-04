using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

/// <summary>
/// Intercepts double-clicking an AudioClip in the Project window and plays it
/// in-editor instead of handing the file off to an external program.
/// Double-click again (or on another clip) to stop the current preview.
/// </summary>
public static class AudioClipDoubleClickHandler
{
    [OnOpenAsset(0)]
    static bool OnOpenAudioClip(EntityId entity)
    {
        var clip = EditorUtility.EntityIdToObject(entity) as AudioClip;
        if (clip == null)
            return false; // not an audio clip — let Unity handle it normally

        if (IsClipPlaying(clip))
            StopAllPreviews();
        else
            PlayClip(clip);

        return true; // we handled it — don't open an external editor
    }

    // AudioUtil lives in the UnityEditor assembly but its API is internal, so it
    // must be reached through reflection. Names differ across Unity versions;
    // resolve them once and fail loud if the running editor doesn't match.
    static readonly Type AudioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil", true);

    static MethodInfo Resolve(params (string name, Type[] args)[] candidates)
    {
        foreach (var (name, args) in candidates)
        {
            var m = AudioUtil.GetMethod(name, BindingFlags.Static | BindingFlags.Public, null, args, null);
            if (m != null)
                return m;
        }
        throw new MissingMethodException(
            $"UnityEditor.AudioUtil: none of [{string.Join(", ", Array.ConvertAll(candidates, c => c.name))}] found for this Unity version.");
    }

    static readonly MethodInfo PlayClipMethod = Resolve(
        ("PlayPreviewClip", new[] { typeof(AudioClip), typeof(int), typeof(bool) }),
        ("PlayClip", new[] { typeof(AudioClip), typeof(int), typeof(bool) }),
        ("PlayClip", new[] { typeof(AudioClip) }));

    static readonly MethodInfo StopAllMethod = Resolve(
        ("StopAllPreviewClips", Type.EmptyTypes),
        ("StopAllClips", Type.EmptyTypes));

    static readonly MethodInfo IsPlayingMethod = Resolve(
        ("IsPreviewClipPlaying", Type.EmptyTypes),
        ("IsClipPlaying", new[] { typeof(AudioClip) }));

    static void PlayClip(AudioClip clip)
    {
        StopAllPreviews();
        var args = PlayClipMethod.GetParameters().Length == 3
            ? new object[] { clip, 0, false }
            : new object[] { clip };
        PlayClipMethod.Invoke(null, args);
    }

    static void StopAllPreviews() => StopAllMethod.Invoke(null, null);

    static bool IsClipPlaying(AudioClip clip)
    {
        var args = IsPlayingMethod.GetParameters().Length == 1 ? new object[] { clip } : null;
        return (bool)IsPlayingMethod.Invoke(null, args);
    }
}
