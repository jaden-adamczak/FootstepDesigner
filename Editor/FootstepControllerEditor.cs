using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(DualFootstepController))]
public class FootstepControllerEditor : Editor
{
    private SerializedProperty footstepMode;
    private SerializedProperty feet;
    private SerializedProperty surfaceProfiles;
    private SerializedProperty useBakedGranularVariations;

    // Auto-detection settings properties
    private SerializedProperty autoDetectSteps;
    private SerializedProperty groundThreshold;
    private SerializedProperty stepCooldown;

    // Raycast Customization properties
    private SerializedProperty raycastDistance;
    private SerializedProperty groundLayer;
    private SerializedProperty raycastOffset;
    private SerializedProperty raycastAngleOffset;

    // Debug properties
    private SerializedProperty showDebugRaycasts;

    // Extra Audio settings properties
    private SerializedProperty spatialBlend;
    private SerializedProperty defaultMixerGroup;
    private SerializedProperty foleyAudioSource;
    private SerializedProperty foleySoundGroups;

    // Foldout state caching (persisted during session)
    private static bool showAutoDetection = false;
    private static bool showRaycastSettings = false;
    private static bool showDebugSettings = false;
    private static bool showFoleySettings = false;

    private void OnEnable()
    {
        footstepMode = serializedObject.FindProperty("footstepMode");
        feet = serializedObject.FindProperty("feet");
        surfaceProfiles = serializedObject.FindProperty("surfaceProfiles");
        useBakedGranularVariations = serializedObject.FindProperty("useBakedGranularVariations");

        autoDetectSteps = serializedObject.FindProperty("autoDetectSteps");
        groundThreshold = serializedObject.FindProperty("groundThreshold");
        stepCooldown = serializedObject.FindProperty("stepCooldown");

        raycastDistance = serializedObject.FindProperty("raycastDistance");
        groundLayer = serializedObject.FindProperty("groundLayer");
        raycastOffset = serializedObject.FindProperty("raycastOffset");
        raycastAngleOffset = serializedObject.FindProperty("raycastAngleOffset");

        showDebugRaycasts = serializedObject.FindProperty("showDebugRaycasts");

        spatialBlend = serializedObject.FindProperty("spatialBlend");
        defaultMixerGroup = serializedObject.FindProperty("defaultMixerGroup");
        foleyAudioSource = serializedObject.FindProperty("foleyAudioSource");
        foleySoundGroups = serializedObject.FindProperty("foleySoundGroups");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Header Styling
        GUIStyle sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
        sectionHeaderStyle.fontSize = 12;
        sectionHeaderStyle.margin = new RectOffset(0, 0, 8, 4);

        // Core Setup
        EditorGUILayout.LabelField("Footstep Controller Setup", sectionHeaderStyle);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.PropertyField(footstepMode);
        EditorGUILayout.PropertyField(feet, new GUIContent("Feet Setup List"), true);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // Active Surface Profiles & Audio options
        EditorGUILayout.LabelField("Profiles & Audio Playback Mode", sectionHeaderStyle);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.PropertyField(surfaceProfiles, new GUIContent("Surface Profiles Mappings"), true);
        EditorGUILayout.PropertyField(useBakedGranularVariations, new GUIContent("Use Baked Granular", "If checked, plays granular Variations synthesized and saved in the profile. If unchecked, plays random base AudioClips with on-the-spot pitch/volume shifting."));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Group 1: Automatic Step Detection Foldout
        showAutoDetection = EditorGUILayout.BeginFoldoutHeaderGroup(showAutoDetection, "Automatic Footstep Detection");
        if (showAutoDetection)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(autoDetectSteps);
            if (autoDetectSteps.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(groundThreshold);
                EditorGUILayout.PropertyField(stepCooldown);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(3);

        // Group 2: Advanced Raycast Settings Foldout
        showRaycastSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showRaycastSettings, "Raycast Customization (Advanced)");
        if (showRaycastSettings)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(raycastDistance);
            
            // Render the LayerMask field nicely
            groundLayer.intValue = LayerMaskField("Ground Layer Mask", groundLayer.intValue);

            EditorGUILayout.PropertyField(raycastOffset);
            EditorGUILayout.PropertyField(raycastAngleOffset);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(3);

        // Group 3: Debug & Visualization Foldout
        showDebugSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showDebugSettings, "Debug Settings");
        if (showDebugSettings)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(showDebugRaycasts);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(3);

        // Group 4: Foley & Audio Routing foldout
        showFoleySettings = EditorGUILayout.BeginFoldoutHeaderGroup(showFoleySettings, "Extra Sounds Settings (Accessory & Gear)");
        if (showFoleySettings)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(spatialBlend);
            EditorGUILayout.PropertyField(defaultMixerGroup);
            EditorGUILayout.PropertyField(foleyAudioSource, new GUIContent("Extra Audio Source"));
            EditorGUILayout.PropertyField(foleySoundGroups, new GUIContent("Extra Sound Groups"), true);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }

    // Helper for rendering clean layer masks
    private static int LayerMaskField(string label, int maskValue)
    {
        List<string> layers = new List<string>();
        List<int> layerNumbers = new List<int>();

        for (int i = 0; i < 32; i++)
        {
            string layerName = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(layerName))
            {
                layers.Add(layerName);
                layerNumbers.Add(i);
            }
        }

        int maskWithoutEmpty = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if (((1 << layerNumbers[i]) & maskValue) > 0)
            {
                maskWithoutEmpty |= (1 << i);
            }
        }

        maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers.ToArray());

        int finalMask = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) > 0)
            {
                finalMask |= (1 << layerNumbers[i]);
            }
        }
        return finalMask;
    }
}
