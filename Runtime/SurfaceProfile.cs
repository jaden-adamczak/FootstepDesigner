using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewSurfaceProfile", menuName = "Footsteps/Surface Profile")]
public class SurfaceProfile : ScriptableObject
{
    [Header("Surface Settings")]
    public string surfaceTag = "Dirt";
    [Tooltip("Physic Materials mapped to this profile.")]
    public List<PhysicsMaterial> physicMaterials = new List<PhysicsMaterial>();
    [Tooltip("Render Materials mapped to this profile.")]
    public List<Material> renderMaterials = new List<Material>();
    public UnityEngine.Audio.AudioMixerGroup customMixerGroup;

    [Header("Left Foot (e.g., Boot)")]
    [Tooltip("Base audio clips for traditional random selection.")]
    public List<AudioClip> leftFootBaseSamples = new List<AudioClip>();
    [Tooltip("Drag your pre-baked granular variations here")]
    public List<AudioClip> leftFootGranularBakes = new List<AudioClip>();
    [Range(0f, 0.5f)] public float leftPitchRandomness = 0.05f;
    [Range(0f, 1f)] public float leftVolumeRandomness = 0.1f;
    [Tooltip("Semitone offset applied to base clips when playing without baked granular variations. +12 = one octave up, -12 = one octave down.")]
    [Range(-12f, 12f)] public float leftBasePitchOffsetSemitones = 0f;

    [Header("Right Foot (e.g., Peg-Leg)")]
    [Tooltip("Base audio clips for traditional random selection.")]
    public List<AudioClip> rightFootBaseSamples = new List<AudioClip>();
    [Tooltip("Drag your pre-baked granular variations here")]
    public List<AudioClip> rightFootGranularBakes = new List<AudioClip>();
    [Range(0f, 0.5f)] public float rightPitchRandomness = 0.05f;
    [Range(0f, 1f)] public float rightVolumeRandomness = 0.1f;
    [Tooltip("Semitone offset applied to base clips when playing without baked granular variations. +12 = one octave up, -12 = one octave down.")]
    [Range(-12f, 12f)] public float rightBasePitchOffsetSemitones = 0f;
    
    // Helper function to get a random baked clip for a specific foot
    public AudioClip GetRandomBake(bool isLeftFoot)
    {
        List<AudioClip> targetList = isLeftFoot ? leftFootGranularBakes : rightFootGranularBakes;
        
        if (targetList == null || targetList.Count == 0) return null;
        return targetList[Random.Range(0, targetList.Count)];
    }

    // Helper to get a random base sample clip for a specific foot
    public AudioClip GetRandomBaseSample(bool isLeftFoot)
    {
        List<AudioClip> targetList = isLeftFoot ? leftFootBaseSamples : rightFootBaseSamples;
        
        if (targetList == null || targetList.Count == 0) return null;
        return targetList[Random.Range(0, targetList.Count)];
    }
}