#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Plays AudioClips inside Editor windows without entering Play Mode.
/// Uses a hidden GameObject with HideFlags.HideAndDontSave so it never
/// appears in the Hierarchy and is never saved to the scene.
/// Works with both asset-backed clips and runtime-created AudioClip.Create clips.
/// </summary>
[InitializeOnLoad]
public static class EditorAudioPlayer
{
    private static GameObject _previewGO;
    private static AudioSource _previewSource;

    static EditorAudioPlayer()
    {
        // Clean up when scripts reload or domain reloads
        AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        EditorApplication.quitting += Cleanup;
    }

    private static void EnsureSource()
    {
        if (_previewSource != null) return;

        // Destroy any stale GO from a previous session
        if (_previewGO != null)
        {
            Object.DestroyImmediate(_previewGO);
            _previewGO = null;
        }

        _previewGO = new GameObject("__EditorAudioPreview__");
        // HideAndDontSave = HideInHierarchy | DontSaveInEditor | DontSaveInBuild | DontUnloadUnusedAsset
        _previewGO.hideFlags = HideFlags.HideAndDontSave;
        _previewSource = _previewGO.AddComponent<AudioSource>();
        _previewSource.playOnAwake = false;
        // Spatial blend 0 = pure 2D so you always hear it in the editor
        _previewSource.spatialBlend = 0f;
        _previewSource.volume = 1f;

        Debug.Log("[EditorAudioPlayer] Preview AudioSource created.");
    }

    /// <summary>
    /// Play a clip immediately. Works with both imported and runtime AudioClips.
    /// Volume and pitch can be overridden.
    /// </summary>
    public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null)
        {
            Debug.LogWarning("[EditorAudioPlayer] Play called with null clip.");
            return;
        }

        EnsureSource();

        _previewSource.Stop();
        _previewSource.pitch = pitch;
        _previewSource.volume = Mathf.Clamp01(volume);
        _previewSource.clip = clip;
        _previewSource.Play();

        Debug.Log($"[EditorAudioPlayer] Playing clip '{clip.name}' — samples: {clip.samples}, channels: {clip.channels}, frequency: {clip.frequency}, volume: {volume:F2}, pitch: {pitch:F2}");
    }

    /// <summary>Stop any currently playing preview.</summary>
    public static void Stop()
    {
        if (_previewSource != null)
            _previewSource.Stop();
    }

    /// <summary>True if a clip is currently playing.</summary>
    public static bool IsPlaying => _previewSource != null && _previewSource.isPlaying;

    public static void Cleanup()
    {
        if (_previewSource != null)
        {
            _previewSource.Stop();
            _previewSource = null;
        }
        if (_previewGO != null)
        {
            Object.DestroyImmediate(_previewGO);
            _previewGO = null;
        }
        Debug.Log("[EditorAudioPlayer] Preview AudioSource cleaned up.");
    }
}
#endif
