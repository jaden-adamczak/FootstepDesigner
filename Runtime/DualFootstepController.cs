using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class DualFootstepController : MonoBehaviour
{
    public enum FootstepMode
    {
        AsymmetricLeftRight, // Plays Left/Right sounds based on each foot's classification
        SymmetricLeftOnly    // All feet use the first sound pack (Left foot)
    }

    [Header("Configuration Mode")]
    public FootstepMode footstepMode = FootstepMode.SymmetricLeftOnly;

    [System.Serializable]
    public class FootSetup
    {
        public string name = "Foot";
        public Transform footBone;
        public AudioSource audioSource;
        [Tooltip("True if this foot maps to the Left Foot sounds in the profiles. False maps to the Right Foot sounds.")]
        public bool isLeft = true;

        [System.NonSerialized] public bool wasGrounded;
        [System.NonSerialized] public float cooldownTimer;
    }

    [Header("Feet Configuration")]
    [Tooltip("Add as many feet as your character has (e.g. 2 for Human, 4 for Horse, etc.).")]
    public List<FootSetup> feet = new List<FootSetup>();

    [Header("Routing & Spatial Settings")]
    public UnityEngine.Audio.AudioMixerGroup defaultMixerGroup;
    [Range(0f, 1f)] public float spatialBlend = 1f;

    [Header("Data")]
    public List<SurfaceProfile> surfaceProfiles;
    public float raycastDistance = 0.5f;
    public LayerMask groundLayer = 1; // Defaults to the "Default" layer (1 << 0)

    [Tooltip("If checked, plays the pre-baked granular synthesis variations from the Profile. If unchecked, plays a random clip from the Base Audio Clips pool and applies the pitch/volume shifting values below on the spot.")]
    public bool useBakedGranularVariations = true;

    [Header("Velocity Scaling")]
    [Tooltip("Character speed considered 100% velocity (fully loud & high-pitched).")]
    public float maxSpeed = 6f;
    [Tooltip("Pitch at minimum speed (0 m/s). Default 0.9.")]
    [Range(0.5f, 1.5f)] public float velocityMinPitch = 0.9f;
    [Tooltip("Pitch at maximum speed. Default 1.1.")]
    [Range(0.5f, 2.0f)] public float velocityMaxPitch = 1.1f;
    [Tooltip("Volume multiplier at minimum speed. Range 0-1. Default 0.5.")]
    [Range(0f, 1f)] public float velocityMinVolume = 0.5f;
    [Tooltip("Volume multiplier at maximum speed. Default 1.0.")]
    [Range(0f, 1f)] public float velocityMaxVolume = 1.0f;

    private Vector3 lastPosition;
    private float currentSpeed;
    
    [Header("Debug Settings")]
    [Tooltip("Show the detection raycast in the Scene view (Green = Hit, Red = Miss).")]
    public bool showDebugRaycasts = true;

    [Header("Automatic Detection")]
    [Tooltip("Automatically trigger footsteps by tracking the foot bone distance to the ground, rather than using Animation Events.")]
    public bool autoDetectSteps = false;
    [Tooltip("Distance from the foot bone to the ground at which a step is triggered.")]
    public float groundThreshold = 0.15f;
    [Tooltip("Minimum time (in seconds) between consecutive footsteps on the same foot to prevent spamming sounds.")]
    public float stepCooldown = 0.3f;

    [Header("Raycast Customization")]
    [Tooltip("Offset applied relative to the foot bone rotation (e.g. to move the ray start to the bottom of the foot).")]
    public Vector3 raycastOffset = Vector3.zero;
    [Tooltip("Rotation offset applied to the raycast direction relative to the character's body orientation.")]
    public Vector3 raycastAngleOffset = Vector3.zero;

    [System.Serializable]
    public class ExtraSoundGroup
    {
        public string name = "Gear Clink";
        public List<AudioClip> clips;
        [Range(0f, 1f)] public float triggerProbability = 0.5f;
        public float minDelay = 0.05f;
        public float maxDelay = 0.2f;
        [Range(0f, 0.5f)] public float pitchRandomness = 0.05f;
        [Range(0f, 0.5f)] public float volumeRandomness = 0.1f;
        
        [Header("Step Filters")]
        public bool triggerOnLeft = true;
        public bool triggerOnRight = true;

        [Header("Audio Routing")]
        [Tooltip("Optional: assign a dedicated AudioSource for this group. Falls back to the foley source, then the foot's own source.")]
        public AudioSource customAudioSource;
    }

    [Header("Extra Sounds (Accessory / Gear)")]
    public AudioSource foleyAudioSource;
    public List<ExtraSoundGroup> foleySoundGroups;

    public void StepLeft()
    {
        // Trigger the first foot marked as Left
        int leftIndex = feet.FindIndex(f => f.isLeft);
        if (leftIndex >= 0)
        {
            StepFoot(leftIndex);
        }
    }

    public void StepRight()
    {
        // Trigger the first foot marked as Right
        int rightIndex = feet.FindIndex(f => !f.isLeft);
        if (rightIndex >= 0)
        {
            StepFoot(rightIndex);
        }
    }

    public void StepFoot(int index)
    {
        if (index < 0 || index >= feet.Count) return;
        var foot = feet[index];
        DetectAndPlay(foot.footBone, foot.audioSource, foot.isLeft);
    }

    private void DetectAndPlay(Transform footTransform, AudioSource source, bool isLeft)
    {
        if (footTransform == null)
        {
            Debug.LogWarning($"[FootstepController] Foot Bone reference is missing on the controller!");
            return;
        }
        if (source == null)
        {
            Debug.LogWarning($"[FootstepController] Audio Source reference is missing on the controller!");
            return;
        }

        Vector3 start = footTransform.position + footTransform.TransformDirection(raycastOffset);
        Vector3 dir = GetRaycastDirection();

        bool hasHit = Physics.Raycast(start, dir, out RaycastHit hit, raycastDistance, groundLayer);

        if (showDebugRaycasts)
        {
            if (hasHit)
            {
                // Draw green line from foot to hit point if it successfully hit the floor
                Debug.DrawLine(start, hit.point, Color.green, 2f);
            }
            else
            {
                // Draw red ray representing full search distance if it missed
                Debug.DrawRay(start, dir * raycastDistance, Color.red, 2f);
            }
        }

        if (hasHit)
        {
            string groundTag = hit.collider.tag;
            PhysicsMaterial hitPhysicMaterial = hit.collider.sharedMaterial;

            List<Material> hitRenderMaterials = new List<Material>();
            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (renderer.sharedMaterials != null)
                {
                    hitRenderMaterials.AddRange(renderer.sharedMaterials);
                }
                else if (renderer.sharedMaterial != null)
                {
                    hitRenderMaterials.Add(renderer.sharedMaterial);
                }
            }

            bool matchedAnyProfile = false;
            foreach (SurfaceProfile profile in surfaceProfiles)
            {
                if (profile == null) continue;

                bool matchesPhysicMaterial = hitPhysicMaterial != null && profile.physicMaterials != null && profile.physicMaterials.Contains(hitPhysicMaterial);
                
                bool matchesRenderMaterial = false;
                if (profile.renderMaterials != null && hitRenderMaterials.Count > 0)
                {
                    foreach (var mat in hitRenderMaterials)
                    {
                        if (mat != null && profile.renderMaterials.Contains(mat))
                        {
                            matchesRenderMaterial = true;
                            break;
                        }
                    }
                }

                bool matchesTag = !string.IsNullOrEmpty(profile.surfaceTag) && profile.surfaceTag != "Untagged" && profile.surfaceTag == groundTag;

                if (matchesPhysicMaterial || matchesRenderMaterial || matchesTag)
                {
                    matchedAnyProfile = true;

                    // Decide what sound classification to check (force Left sounds if SymmetricLeftOnly is selected)
                    bool forceLeft = (footstepMode == FootstepMode.SymmetricLeftOnly) ? true : isLeft;

                    AudioClip clipToPlay = null;
                    float normSpeed = Mathf.Clamp01(currentSpeed / maxSpeed);

                    if (useBakedGranularVariations)
                    {
                        var bakes = forceLeft ? profile.leftFootGranularBakes : profile.rightFootGranularBakes;
                        if (bakes != null && bakes.Count > 0)
                        {
                            int idx = Mathf.Clamp(Mathf.RoundToInt(normSpeed * (bakes.Count - 1)), 0, bakes.Count - 1);
                            int jitter = Random.Range(-1, 2);
                            idx = Mathf.Clamp(idx + jitter, 0, bakes.Count - 1);
                            clipToPlay = bakes[idx];
                        }
                    }
                    
                    if (clipToPlay == null)
                    {
                        clipToPlay = profile.GetRandomBaseSample(forceLeft);
                    }
                    
                    if (clipToPlay != null)
                    {
                        float volVariation = forceLeft ? profile.leftVolumeRandomness : profile.rightVolumeRandomness;
                        float pitchVariation = forceLeft ? profile.leftPitchRandomness : profile.rightPitchRandomness;
                        float pitchOffsetSemitones = forceLeft ? profile.leftBasePitchOffsetSemitones : profile.rightBasePitchOffsetSemitones;

                        source.spatialBlend = spatialBlend;
                        source.outputAudioMixerGroup = profile.customMixerGroup != null ? profile.customMixerGroup : defaultMixerGroup;

                        // Velocity-scaled pitch and volume using tunable curve settings
                        float basePitch = Mathf.Lerp(velocityMinPitch, velocityMaxPitch, normSpeed);
                        // Semitone offset (only applied when using base clips, not bakes which are already pitched)
                        float semitoneMultiplier = (clipToPlay != null && !useBakedGranularVariations) 
                            ? Mathf.Pow(2f, pitchOffsetSemitones / 12f) 
                            : 1f;
                        source.pitch = (basePitch + Random.Range(-pitchVariation, pitchVariation) * (1f + normSpeed * 0.3f)) * semitoneMultiplier;
                        source.volume = Mathf.Lerp(velocityMinVolume, velocityMaxVolume, normSpeed) - Random.Range(0f, volVariation) * (1f - normSpeed * 0.2f);
                        
                        source.PlayOneShot(clipToPlay);

                        if (PerformanceTracker.Instance != null)
                        {
                            PerformanceTracker.Instance.LogStepEvent(isLeft, currentSpeed, profile.surfaceTag);
                        }

                        TriggerExtraSounds(forceLeft, profile.customMixerGroup, normSpeed, source);

                    }
                    else
                    {
                        if (PlayerPrefs.GetInt("FootstepDesigner_MuteNoClips", 0) != 1)
                        {
                            Debug.LogWarning($"[FootstepController] Matched profile '{profile.name}' for {hit.collider.name}, but no audio clips are assigned to this profile.");
                        }
                    }
                    break;
                }
            }

            if (!matchedAnyProfile)
            {
                if (PlayerPrefs.GetInt("FootstepDesigner_MuteNoProfileMatch", 0) != 1)
                {
                    string registered = "";
                    if (surfaceProfiles != null)
                    {
                        var names = new List<string>();
                        for (int p = 0; p < surfaceProfiles.Count; p++)
                        {
                            if (surfaceProfiles[p] != null) names.Add(surfaceProfiles[p].name);
                        }
                        registered = string.Join(", ", names);
                    }
                    Debug.LogWarning($"[FootstepController] Stepped on '{hit.collider.name}' (Tag: {groundTag}, PhysicMaterial: {(hitPhysicMaterial != null ? hitPhysicMaterial.name : "None")}), but no matching SurfaceProfile was found. controller registered profiles: [{registered}]");
                }
            }
        }
        else
        {
            if (PlayerPrefs.GetInt("FootstepDesigner_MuteRaycastMiss", 0) != 1)
            {
                Debug.LogWarning($"[FootstepController] Raycast from '{footTransform.name}' missed the ground. The floor might be too far away (Raycast Distance: {raycastDistance}) or the floor's layer is not included in the groundLayer mask.");
            }
        }
    }

    private void TriggerExtraSounds(bool isLeftStep, UnityEngine.Audio.AudioMixerGroup surfaceMixer, float normSpeed, AudioSource footSource)
    {
        if (foleySoundGroups == null) return;

        foreach (var group in foleySoundGroups)
        {
            if (group.clips == null || group.clips.Count == 0) continue;

            // Apply step filters
            if (isLeftStep && !group.triggerOnLeft) continue;
            if (!isLeftStep && !group.triggerOnRight) continue;

            if (Random.value <= group.triggerProbability)
            {
                AudioClip extraClip = group.clips[Random.Range(0, group.clips.Count)];
                float delay = Random.Range(group.minDelay, group.maxDelay);

                UnityEngine.Audio.AudioMixerGroup targetMixer = surfaceMixer != null ? surfaceMixer : defaultMixerGroup;

                // Resolve audio source priority: group-specific > foley > foot's own
                AudioSource effectiveSource = group.customAudioSource != null ? group.customAudioSource
                    : foleyAudioSource != null ? foleyAudioSource
                    : footSource;

                if (effectiveSource == null) continue;

                StartCoroutine(PlayExtraSoundDelayed(extraClip, delay, group.pitchRandomness, group.volumeRandomness, targetMixer, normSpeed, effectiveSource));
            }
        }
    }

    private IEnumerator PlayExtraSoundDelayed(AudioClip clip, float delay, float pitchRand, float volRand, UnityEngine.Audio.AudioMixerGroup targetMixer, float normSpeed, AudioSource source)
    {
        yield return new WaitForSeconds(delay);
        if (source != null && clip != null)
        {
            source.spatialBlend = spatialBlend;
            source.outputAudioMixerGroup = targetMixer;

            // Mirror the main footstep velocity scaling so clanks feel proportional to movement speed
            source.pitch = 1f + Random.Range(-pitchRand, pitchRand);
            source.volume = (0.5f + 0.5f * normSpeed) - Random.Range(0f, volRand) * (1f - normSpeed * 0.2f);
            source.PlayOneShot(clip);
        }
    }

    private void Start()
    {
        lastPosition = transform.position;
    }

    private void Update()
    {
        Vector3 currPos = transform.position;
        if (Time.deltaTime > 0f)
        {
            currentSpeed = ((currPos - lastPosition) / Time.deltaTime).magnitude;
        }
        lastPosition = currPos;

        if (autoDetectSteps)
        {
            for (int i = 0; i < feet.Count; i++)
            {
                var foot = feet[i];
                if (foot == null) continue;

                if (foot.cooldownTimer > 0f) foot.cooldownTimer -= Time.deltaTime;

                CheckAutoStep(foot);
            }
        }
    }

    private void CheckAutoStep(FootSetup foot)
    {
        if (foot == null || foot.footBone == null || foot.audioSource == null) return;

        Vector3 start = foot.footBone.position + foot.footBone.TransformDirection(raycastOffset);
        Vector3 dir = GetRaycastDirection();

        bool isGroundedNow = false;

        if (Physics.Raycast(start, dir, out RaycastHit hit, raycastDistance, groundLayer))
        {
            if (hit.distance <= groundThreshold)
            {
                isGroundedNow = true;
            }
        }

        // Trigger step if the foot just landed and the cooldown is over
        if (isGroundedNow && !foot.wasGrounded && foot.cooldownTimer <= 0f)
        {
            DetectAndPlay(foot.footBone, foot.audioSource, foot.isLeft);
            foot.cooldownTimer = stepCooldown;
        }

        foot.wasGrounded = isGroundedNow;
    }

    public Vector3 GetRaycastDirection()
    {
        // Direction is world down rotated by the character's orientation and our custom euler angles
        Quaternion rotation = transform.rotation * Quaternion.Euler(raycastAngleOffset);
        return rotation * Vector3.down;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugRaycasts) return;

        for (int i = 0; i < feet.Count; i++)
        {
            var foot = feet[i];
            if (foot != null && foot.footBone != null)
            {
                GizmosDrawFootRay(foot.footBone);
            }
        }
    }

    private void GizmosDrawFootRay(Transform footTransform)
    {
        if (footTransform == null) return;

        Vector3 start = footTransform.position + footTransform.TransformDirection(raycastOffset);
        Vector3 dir = GetRaycastDirection();

        bool hasHit = Physics.Raycast(start, dir, out RaycastHit hit, raycastDistance, groundLayer);

        Gizmos.color = hasHit ? Color.green : Color.red;
        if (hasHit)
        {
            Gizmos.DrawLine(start, hit.point);
            Gizmos.DrawWireSphere(hit.point, 0.03f);
        }
        else
        {
            Gizmos.DrawRay(start, dir * raycastDistance);
        }
    }
}