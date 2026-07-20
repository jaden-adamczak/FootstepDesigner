using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class FootstepDesignerWindow : EditorWindow
{
    private int activeTab = 0;
    private string[] tabNames = { "Profiles", "Scene Controller", "Audio Effects Station", "Settings", "Credits & License" };

    // Profiles Tab variables
    private List<SurfaceProfile> allProfiles = new List<SurfaceProfile>();
    private SurfaceProfile selectedProfile;
    private Vector2 sidebarScroll;
    private Vector2 detailsScroll;
    private string searchString = "";
    private string newProfileTag = "";
    private float sidebarWidth = 220f;
    private bool isResizingSidebar = false;
    private bool sidebarCollapsed = false;

    // Granular Baker variables
    public enum TargetFoot { Left, Right, Both }
    private TargetFoot targetFoot = TargetFoot.Both;
    private int variationCount = 5;
    private float pitchDeviation = 2.0f;
    private float grainSizeMs = 50.0f;
    private float overlapPercent = 75.0f;
    private bool clearExistingBakes = false;
    private bool showLeftBakes = true;
    private bool showRightBakes = true;
    private bool showLeftBaseClips = true;
    private bool showRightBaseClips = true;

    // Scene Controller Tab variables
    private DualFootstepController activeController;
    private Vector2 controllerScroll;
    private bool showGeneralSettings = true;
    private bool showRaycastSettings = true;
    private bool showBoneSettings = true;
    private bool showFoleySettings = true;

    // Audio Effects Station variables
    private AudioClip effectSourceClip;
    private List<IFootstepDSP> dspPlugins = new List<IFootstepDSP>();
    private AudioClip previewProcessedClip;
    private Vector2 effectsScroll;
    private float effectsMasterGain = 1.0f;
    private float[] effectsAmplitudeEnvelope;

    // Granular Baker gain
    private float bakerMasterGain = 1.0f;

    // Credits Tab variables
    private Vector2 licenseScroll;
    private const string MIT_LICENSE_TEXT = 
        "MIT License\n\n" +
        "Copyright (c) 2026 Jaden Adamczak\n\n" +
        "Permission is hereby granted, free of charge, to any person obtaining a copy " +
        "of this software and associated documentation files (the \"Software\"), to deal " +
        "in the Software without restriction, including without limitation the rights " +
        "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell " +
        "copies of the Software, and to permit persons to whom the Software is " +
        "furnished to do so, subject to the following conditions:\n\n" +
        "The above copyright notice and this permission notice shall be included in all " +
        "copies or substantial portions of the Software.\n\n" +
        "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR " +
        "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, " +
        "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE " +
        "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER " +
        "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, " +
        "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE " +
        "SOFTWARE.";

    [MenuItem("Tools/Footsteps/Footstep Designer")]
    public static void ShowWindow()
    {
        GetWindow<FootstepDesignerWindow>("Footstep Designer");
    }

    private void OnEnable()
    {
        RefreshProfiles();
        InitializeDSPPlugins();
    }

    private void InitializeDSPPlugins()
    {
        dspPlugins.Clear();
        var assembly = typeof(IFootstepDSP).Assembly;
        var types = assembly.GetTypes();
        foreach (var type in types)
        {
            if (typeof(IFootstepDSP).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract && type != typeof(TemplateDSP))
            {
                try
                {
                    IFootstepDSP plugin = (IFootstepDSP)System.Activator.CreateInstance(type);
                    dspPlugins.Add(plugin);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("failed to instantiate dsp plugin: " + e.Message);
                }
            }
        }
    }

    private void RefreshProfiles()
    {
        allProfiles.Clear();
        string[] guids = AssetDatabase.FindAssets("t:SurfaceProfile");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SurfaceProfile p = AssetDatabase.LoadAssetAtPath<SurfaceProfile>(path);
            if (p != null)
            {
                allProfiles.Add(p);
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Space(5);
        activeTab = GUILayout.Toolbar(activeTab, tabNames);
        GUILayout.Space(10);

        switch (activeTab)
        {
            case 0:
                DrawProfilesTab();
                break;
            case 1:
                DrawControllerTab();
                break;
            case 2:
                DrawEffectsStationTab();
                break;
            case 3:
                DrawSettingsTab();
                break;
            case 4:
                DrawCreditsTab();
                break;
        }
    }

    // --- Tab 0: Profiles Designer ---
    private void DrawProfilesTab()
    {
        GUIStyle activeStyle = new GUIStyle(EditorStyles.miniButton);
        activeStyle.fontStyle = FontStyle.Bold;
        if (EditorGUIUtility.isProSkin)
        {
            activeStyle.normal.textColor = Color.yellow;
            activeStyle.active.textColor = Color.yellow;
        }
        else
        {
            activeStyle.normal.textColor = new Color(0.1f, 0.4f, 0.8f);
            activeStyle.active.textColor = new Color(0.1f, 0.4f, 0.8f);
        }

        GUIStyle normalStyle = new GUIStyle(EditorStyles.miniButton);

        EditorGUILayout.BeginHorizontal();

        if (sidebarCollapsed)
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(30), GUILayout.ExpandHeight(true));
            if (GUILayout.Button("▶", GUILayout.Width(20), GUILayout.Height(30)))
            {
                sidebarCollapsed = false;
            }
            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(sidebarWidth), GUILayout.ExpandHeight(true));
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Surface Profiles", EditorStyles.boldLabel);
            if (GUILayout.Button("◀", GUILayout.Width(20)))
            {
                sidebarCollapsed = true;
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            searchString = EditorGUILayout.TextField("Search", searchString);
            GUILayout.Space(5);

            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll, GUILayout.ExpandHeight(true));
            foreach (var profile in allProfiles)
            {
                if (profile == null) continue;

                if (!string.IsNullOrEmpty(searchString))
                {
                    bool matchesTag = profile.surfaceTag.ToLower().Contains(searchString.ToLower());
                    bool matchesName = profile.name.ToLower().Contains(searchString.ToLower());
                    if (!matchesTag && !matchesName) continue;
                }

                GUIStyle style = (selectedProfile == profile) ? activeStyle : normalStyle;
                string displayName = string.IsNullOrEmpty(profile.surfaceTag) ? "[No Tag]" : profile.surfaceTag;
                
                if (GUILayout.Button($"{displayName} ({profile.name})", style, GUILayout.Height(25)))
                {
                    selectedProfile = profile;
                    GUI.FocusControl("");
                }
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(10);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Create New Profile", EditorStyles.boldLabel);
            newProfileTag = EditorGUILayout.TextField("Surface Tag", newProfileTag);
            if (GUILayout.Button("Create Profile"))
            {
                CreateNewProfile(newProfileTag);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();

            Rect splitterRect = GUILayoutUtility.GetRect(5, 0, GUILayout.Width(5), GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);
            GUI.Box(new Rect(splitterRect.x + 2, splitterRect.y, 1, splitterRect.height), "");

            if (Event.current != null)
            {
                if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
                {
                    isResizingSidebar = true;
                }
                if (isResizingSidebar)
                {
                    sidebarWidth = Event.current.mousePosition.x;
                    sidebarWidth = Mathf.Clamp(sidebarWidth, 120f, 400f);
                    Repaint();
                }
                if (Event.current.type == EventType.MouseUp)
                {
                    isResizingSidebar = false;
                }
            }
        }

        // Right Detail Editor Panel
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (selectedProfile == null)
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Select or create a Surface Profile on the left.", EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }
        else
        {
            detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll, GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Profile: {selectedProfile.name}", EditorStyles.boldLabel);
            
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Delete Profile", GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog("Delete Profile", $"Delete profile '{selectedProfile.name}'? This cannot be undone.", "Delete", "Cancel"))
                {
                    DeleteSelectedProfile();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                    return;
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Asset Path: {AssetDatabase.GetAssetPath(selectedProfile)}", EditorStyles.miniLabel);
            GUILayout.Space(10);

            var controller = GameObject.FindAnyObjectByType<DualFootstepController>();
            if (controller != null)
            {
                bool inList = controller.surfaceProfiles != null && controller.surfaceProfiles.Contains(selectedProfile);
                if (inList)
                {
                    GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
                    if (GUILayout.Button("Remove from Scene Controller Profile List", GUILayout.Height(25)))
                    {
                        controller.surfaceProfiles.Remove(selectedProfile);
                        EditorUtility.SetDirty(controller);
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
                    if (GUILayout.Button("Add to Scene Controller Profile List", GUILayout.Height(25)))
                    {
                        if (controller.surfaceProfiles == null)
                        {
                            controller.surfaceProfiles = new List<SurfaceProfile>();
                        }
                        controller.surfaceProfiles.Add(selectedProfile);
                        EditorUtility.SetDirty(controller);
                    }
                    GUI.backgroundColor = Color.white;
                }
                GUILayout.Space(10);
            }

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.HelpBox("Specify at least ONE of the three checks below (Tag, Physic Material, or Render Material) to match this profile on raycast hit. Any single match will trigger this surface profile.", MessageType.Info);
            GUILayout.Space(5);

            // Settings Tag
            EditorGUILayout.BeginHorizontal();
            selectedProfile.surfaceTag = EditorGUILayout.TextField("Surface Tag Mapping", selectedProfile.surfaceTag);
            DrawInfoIcon("Tag mapped on Colliders (e.g. 'Wood'). Ground hit tag matches play settings.");
            EditorGUILayout.EndHorizontal();

            // Physic Materials mapping
            EditorGUILayout.LabelField("Physic Material Mappings", EditorStyles.boldLabel);
            if (selectedProfile.physicMaterials == null)
                selectedProfile.physicMaterials = new List<PhysicsMaterial>();
            
            EditorGUILayout.BeginVertical("box");
            if (GUILayout.Button("Add Physic Material Mapping"))
            {
                selectedProfile.physicMaterials.Add(null);
            }
            for (int i = 0; i < selectedProfile.physicMaterials.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                selectedProfile.physicMaterials[i] = (PhysicsMaterial)EditorGUILayout.ObjectField($"Physic Material {i + 1}", selectedProfile.physicMaterials[i], typeof(PhysicsMaterial), false);
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    selectedProfile.physicMaterials.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            // Render Materials mapping
            EditorGUILayout.LabelField("Render Material Mappings", EditorStyles.boldLabel);
            if (selectedProfile.renderMaterials == null)
                selectedProfile.renderMaterials = new List<Material>();

            EditorGUILayout.BeginVertical("box");
            if (GUILayout.Button("Add Render Material Mapping"))
            {
                selectedProfile.renderMaterials.Add(null);
            }
            for (int i = 0; i < selectedProfile.renderMaterials.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                selectedProfile.renderMaterials[i] = (Material)EditorGUILayout.ObjectField($"Render Material {i + 1}", selectedProfile.renderMaterials[i], typeof(Material), false);
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    selectedProfile.renderMaterials.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            
            // Spatial Routing
            EditorGUILayout.BeginHorizontal();
            selectedProfile.customMixerGroup = (UnityEngine.Audio.AudioMixerGroup)EditorGUILayout.ObjectField("Custom Mixer Group", selectedProfile.customMixerGroup, typeof(UnityEngine.Audio.AudioMixerGroup), false);
            DrawInfoIcon("Optional Mixer routing override for this surface. Falls back to controller mixer if null.");
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10);

            // Left Foot details
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Left Foot Audio Config (Pool)", EditorStyles.boldLabel);
            selectedProfile.leftPitchRandomness = EditorGUILayout.Slider("Pitch Randomness", selectedProfile.leftPitchRandomness, 0f, 0.5f);
            selectedProfile.leftVolumeRandomness = EditorGUILayout.Slider("Volume Randomness", selectedProfile.leftVolumeRandomness, 0f, 1f);

            EditorGUILayout.BeginHorizontal();
            selectedProfile.leftBasePitchOffsetSemitones = EditorGUILayout.Slider("Base Clip Pitch Offset (semitones)", selectedProfile.leftBasePitchOffsetSemitones, -12f, 12f);
            DrawInfoIcon("Shifts the pitch of base clips in semitones when playing without baked granular variations. +12 = one octave up.");
            EditorGUILayout.EndHorizontal();
            
            // Base Samples List
            if (selectedProfile.leftFootBaseSamples == null)
                selectedProfile.leftFootBaseSamples = new List<AudioClip>();

            showLeftBaseClips = EditorGUILayout.Foldout(showLeftBaseClips, $"Base Audio Clips ({selectedProfile.leftFootBaseSamples.Count})");
            if (showLeftBaseClips)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("Add Base AudioClip"))
                {
                    selectedProfile.leftFootBaseSamples.Add(null);
                }
                for (int i = 0; i < selectedProfile.leftFootBaseSamples.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    selectedProfile.leftFootBaseSamples[i] = (AudioClip)EditorGUILayout.ObjectField($"Clip {i + 1}", selectedProfile.leftFootBaseSamples[i], typeof(AudioClip), false);
                    if (GUILayout.Button("Play", GUILayout.Width(45)))
                    {
                        PlayClip(selectedProfile.leftFootBaseSamples[i]);
                    }
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        selectedProfile.leftFootBaseSamples.RemoveAt(i);
                        EditorGUI.indentLevel--;
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(5);

            showLeftBakes = EditorGUILayout.Foldout(showLeftBakes, $"Baked Granular Variations ({selectedProfile.leftFootGranularBakes.Count})");
            if (showLeftBakes)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < selectedProfile.leftFootGranularBakes.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    selectedProfile.leftFootGranularBakes[i] = (AudioClip)EditorGUILayout.ObjectField($"Variation {i + 1}", selectedProfile.leftFootGranularBakes[i], typeof(AudioClip), false);
                    if (GUILayout.Button("Play", GUILayout.Width(45)))
                    {
                        PlayClip(selectedProfile.leftFootGranularBakes[i]);
                    }
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        selectedProfile.leftFootGranularBakes.RemoveAt(i);
                        EditorGUI.indentLevel--;
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Right Foot details
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Right Foot Audio Config (Pool)", EditorStyles.boldLabel);
            selectedProfile.rightPitchRandomness = EditorGUILayout.Slider("Pitch Randomness", selectedProfile.rightPitchRandomness, 0f, 0.5f);
            selectedProfile.rightVolumeRandomness = EditorGUILayout.Slider("Volume Randomness", selectedProfile.rightVolumeRandomness, 0f, 1f);

            EditorGUILayout.BeginHorizontal();
            selectedProfile.rightBasePitchOffsetSemitones = EditorGUILayout.Slider("Base Clip Pitch Offset (semitones)", selectedProfile.rightBasePitchOffsetSemitones, -12f, 12f);
            DrawInfoIcon("Shifts the pitch of base clips in semitones when playing without baked granular variations. +12 = one octave up.");
            EditorGUILayout.EndHorizontal();

            // Base Samples List
            if (selectedProfile.rightFootBaseSamples == null)
                selectedProfile.rightFootBaseSamples = new List<AudioClip>();

            showRightBaseClips = EditorGUILayout.Foldout(showRightBaseClips, $"Base Audio Clips ({selectedProfile.rightFootBaseSamples.Count})");
            if (showRightBaseClips)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("Add Base AudioClip"))
                {
                    selectedProfile.rightFootBaseSamples.Add(null);
                }
                for (int i = 0; i < selectedProfile.rightFootBaseSamples.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    selectedProfile.rightFootBaseSamples[i] = (AudioClip)EditorGUILayout.ObjectField($"Clip {i + 1}", selectedProfile.rightFootBaseSamples[i], typeof(AudioClip), false);
                    if (GUILayout.Button("Play", GUILayout.Width(45)))
                    {
                        PlayClip(selectedProfile.rightFootBaseSamples[i]);
                    }
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        selectedProfile.rightFootBaseSamples.RemoveAt(i);
                        EditorGUI.indentLevel--;
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(5);

            showRightBakes = EditorGUILayout.Foldout(showRightBakes, $"Baked Granular Variations ({selectedProfile.rightFootGranularBakes.Count})");
            if (showRightBakes)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < selectedProfile.rightFootGranularBakes.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    selectedProfile.rightFootGranularBakes[i] = (AudioClip)EditorGUILayout.ObjectField($"Variation {i + 1}", selectedProfile.rightFootGranularBakes[i], typeof(AudioClip), false);
                    if (GUILayout.Button("Play", GUILayout.Width(45)))
                    {
                        PlayClip(selectedProfile.rightFootGranularBakes[i]);
                    }
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        selectedProfile.rightFootGranularBakes.RemoveAt(i);
                        EditorGUI.indentLevel--;
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(selectedProfile);
                AssetDatabase.SaveAssets();
            }

            GUILayout.Space(15);

            // Granular Baker Section
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Granular Baker Synthesis", EditorStyles.boldLabel);
            
            targetFoot = (TargetFoot)EditorGUILayout.EnumPopup("Target Foot", targetFoot);
            
            EditorGUILayout.BeginHorizontal();
            variationCount = EditorGUILayout.IntSlider("Variations Per Clip", variationCount, 1, 20);
            DrawInfoIcon("Number of unique WAV variations to generate per base clip. All base clips in the pool are baked.");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            pitchDeviation = EditorGUILayout.Slider("Pitch Deviation (± Semitones)", pitchDeviation, 0.1f, 6.0f);
            DrawInfoIcon("Pitch shift boundary limits in semitones.");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            grainSizeMs = EditorGUILayout.Slider("Grain Size (ms)", grainSizeMs, 10.0f, 200.0f);
            DrawInfoIcon("Duration of granular audio grains. 40-70ms is standard.");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            overlapPercent = EditorGUILayout.Slider("Overlap (%)", overlapPercent, 50.0f, 90.0f);
            DrawInfoIcon("Overlay space between grains. 75%+ ensures smoothness.");
            EditorGUILayout.EndHorizontal();
            
            clearExistingBakes = EditorGUILayout.Toggle("Clear Existing Bakes", clearExistingBakes);

            EditorGUILayout.BeginHorizontal();
            bakerMasterGain = EditorGUILayout.Slider("Master Gain", bakerMasterGain, 0.1f, 3.0f);
            DrawInfoIcon("Multiply the final sample amplitude before writing to disk. Use > 1.0 to boost quiet samples (e.g. grass), < 1.0 to reduce hot signals.");
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            if (GUILayout.Button("Bake Variations", GUILayout.Height(30)))
            {
                RunBaking();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    // --- Tab 1: Scene Controller Editor ---
    private void DrawControllerTab()
    {
        if (activeController == null)
        {
            activeController = FindAnyObjectByType<DualFootstepController>();
        }

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Scene Controller Configuration", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        activeController = (DualFootstepController)EditorGUILayout.ObjectField("Active Controller", activeController, typeof(DualFootstepController), true);
        DrawInfoIcon("Assign the active controller attached to the character GameObject in your scene.");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        if (activeController == null)
        {
            EditorGUILayout.HelpBox("Please assign or select a DualFootstepController in the active scene.", MessageType.Warning);
            return;
        }

        controllerScroll = EditorGUILayout.BeginScrollView(controllerScroll);
        EditorGUI.BeginChangeCheck();

        // 1. General Config & Audio Routing
        showGeneralSettings = EditorGUILayout.Foldout(showGeneralSettings, "General Configuration & Audio Routing");
        if (showGeneralSettings)
        {
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            activeController.footstepMode = (DualFootstepController.FootstepMode)EditorGUILayout.EnumPopup("Footstep Mode", activeController.footstepMode);
            DrawInfoIcon("AsymmetricLeftRight: Plays Left/Right sounds based on each foot's classification.\nSymmetricLeftOnly: Forces all feet to use the first sound pack (Left foot).");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            activeController.useBakedGranularVariations = EditorGUILayout.Toggle("Use Baked Granular", activeController.useBakedGranularVariations);
            DrawInfoIcon("If enabled, plays synthesized granular variations from the profile. If disabled, plays random base clips with pitch/volume randomness applied on-the-spot (traditional pitch shifting).");
            EditorGUILayout.EndHorizontal();

            if (!activeController.useBakedGranularVariations)
            {
                EditorGUILayout.HelpBox("Traditional mode active: a random base clip is selected per step and pitch-shifted in real-time. No baking required. Use Base Clip Pitch Offset (on each Surface Profile) to tune the register of your samples.", MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();
            activeController.spatialBlend = EditorGUILayout.Slider("Spatial Blend (2D/3D)", activeController.spatialBlend, 0f, 1f);
            DrawInfoIcon("0 = Stereo 2D. 1 = Spatialized 3D (highly recommended for VR).");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            activeController.defaultMixerGroup = (UnityEngine.Audio.AudioMixerGroup)EditorGUILayout.ObjectField("Default Mixer Group", activeController.defaultMixerGroup, typeof(UnityEngine.Audio.AudioMixerGroup), false);
            DrawInfoIcon("Default Audio Mixer Group destination.");
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);
            EditorGUILayout.LabelField("Velocity Curve", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            activeController.maxSpeed = EditorGUILayout.FloatField("Max Speed (m/s)", activeController.maxSpeed);
            DrawInfoIcon("Character speed (m/s) considered 100% velocity — fully loud and highest pitch.");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Pitch Range (Min → Max)", GUILayout.Width(180));
            activeController.velocityMinPitch = EditorGUILayout.FloatField(activeController.velocityMinPitch, GUILayout.Width(50));
            EditorGUILayout.LabelField("→", GUILayout.Width(16));
            activeController.velocityMaxPitch = EditorGUILayout.FloatField(activeController.velocityMaxPitch, GUILayout.Width(50));
            DrawInfoIcon("AudioSource.pitch interpolated from min (slow) to max (fast). 1.0 = no shift.");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Volume Range (Min → Max)", GUILayout.Width(180));
            activeController.velocityMinVolume = EditorGUILayout.FloatField(activeController.velocityMinVolume, GUILayout.Width(50));
            EditorGUILayout.LabelField("→", GUILayout.Width(16));
            activeController.velocityMaxVolume = EditorGUILayout.FloatField(activeController.velocityMaxVolume, GUILayout.Width(50));
            DrawInfoIcon("Volume interpolated from min (slow) to max (fast). 1.0 = full.");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        // 2. Raycasting Settings
        showRaycastSettings = EditorGUILayout.Foldout(showRaycastSettings, "Raycast & Ground Settings");
        if (showRaycastSettings)
        {
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            activeController.raycastDistance = EditorGUILayout.FloatField("Raycast Distance", activeController.raycastDistance);
            DrawInfoIcon("Length of down-ray cast under the foot bone to check surface tags.");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            activeController.groundLayer = LayerMaskField("Ground Layer Mask", activeController.groundLayer);
            DrawInfoIcon("Physics layers parsed by raycasts. Exclude character meshes.");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        // 3. Dynamic Feet list
        showBoneSettings = EditorGUILayout.Foldout(showBoneSettings, "Feet bones & Audio Sources List");
        if (showBoneSettings)
        {
            EditorGUILayout.BeginVertical("box");
            if (activeController.feet == null)
            {
                activeController.feet = new List<DualFootstepController.FootSetup>();
            }

            if (GUILayout.Button("Add Foot setup"))
            {
                activeController.feet.Add(new DualFootstepController.FootSetup { name = $"Foot {activeController.feet.Count + 1}" });
            }
            GUILayout.Space(5);

            for (int i = 0; i < activeController.feet.Count; i++)
            {
                var foot = activeController.feet[i];
                EditorGUILayout.BeginVertical("box");
                
                EditorGUILayout.BeginHorizontal();
                foot.name = EditorGUILayout.TextField("Foot Name", foot.name);
                GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    activeController.feet.RemoveAt(i);
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                foot.footBone = (Transform)EditorGUILayout.ObjectField("Foot Bone (Transform)", foot.footBone, typeof(Transform), true);
                foot.audioSource = (AudioSource)EditorGUILayout.ObjectField("Audio Source", foot.audioSource, typeof(AudioSource), true);
                foot.isLeft = EditorGUILayout.Toggle("Is Left Classification", foot.isLeft);

                EditorGUILayout.EndVertical();
                GUILayout.Space(3);
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        // 4. Extra Sound Settings (renamed from Foley)
        showFoleySettings = EditorGUILayout.Foldout(showFoleySettings, $"Extra Sounds Config ({activeController.foleySoundGroups?.Count ?? 0} groups)");
        if (showFoleySettings)
        {
            EditorGUILayout.BeginVertical("box");
            
            if (activeController.foleySoundGroups == null)
            {
                activeController.foleySoundGroups = new List<DualFootstepController.ExtraSoundGroup>();
            }

            if (GUILayout.Button("Add Extra Sound Group"))
            {
                activeController.foleySoundGroups.Add(new DualFootstepController.ExtraSoundGroup
                {
                    name = "Gear Clink",
                    clips = new List<AudioClip>(),
                    triggerProbability = 0.5f,
                    minDelay = 0.05f,
                    maxDelay = 0.2f,
                    pitchRandomness = 0.05f,
                    volumeRandomness = 0.1f,
                    triggerOnLeft = true,
                    triggerOnRight = true
                });
            }
            GUILayout.Space(5);

            for (int g = 0; g < activeController.foleySoundGroups.Count; g++)
            {
                var group = activeController.foleySoundGroups[g];
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                group.name = EditorGUILayout.TextField("Group Name", group.name);
                GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    activeController.foleySoundGroups.RemoveAt(g);
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                group.triggerProbability = EditorGUILayout.Slider("Trigger Probability", group.triggerProbability, 0f, 1f);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Delay Range (Min/Max)", GUILayout.Width(150));
                group.minDelay = EditorGUILayout.FloatField(group.minDelay, GUILayout.Width(50));
                group.maxDelay = EditorGUILayout.FloatField(group.maxDelay, GUILayout.Width(50));
                DrawInfoIcon("Seconds to wait after step impact before playing the extra audio.");
                EditorGUILayout.EndHorizontal();

                group.pitchRandomness = EditorGUILayout.Slider("Pitch Randomness", group.pitchRandomness, 0f, 0.5f);
                group.volumeRandomness = EditorGUILayout.Slider("Volume Randomness", group.volumeRandomness, 0f, 0.5f);

                EditorGUILayout.BeginHorizontal();
                group.triggerOnLeft = EditorGUILayout.Toggle("Trigger on Left", group.triggerOnLeft);
                group.triggerOnRight = EditorGUILayout.Toggle("Trigger on Right", group.triggerOnRight);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                group.customAudioSource = (AudioSource)EditorGUILayout.ObjectField("Custom Audio Source", group.customAudioSource, typeof(AudioSource), true);
                DrawInfoIcon("Optional dedicated AudioSource for this group. Falls back to the controller's Foley source, then the triggering foot's source.");
                EditorGUILayout.EndHorizontal();

                // Clips list
                EditorGUILayout.LabelField("Audio Clips", EditorStyles.boldLabel);
                if (GUILayout.Button("Add Clip", GUILayout.Width(100)))
                {
                    group.clips.Add(null);
                }

                for (int c = 0; c < group.clips.Count; c++)
                {
                    EditorGUILayout.BeginHorizontal();
                    group.clips[c] = (AudioClip)EditorGUILayout.ObjectField($"Clip {c + 1}", group.clips[c], typeof(AudioClip), false);
                    if (GUILayout.Button("Play", GUILayout.Width(45)))
                    {
                        PlayClip(group.clips[c]);
                    }
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        group.clips.RemoveAt(c);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(5);
            }
            EditorGUILayout.EndVertical();
        }

        // Active surface profile list on controller
        EditorGUILayout.LabelField("Active Profiles List", EditorStyles.boldLabel);
        if (activeController.surfaceProfiles == null)
        {
            activeController.surfaceProfiles = new List<SurfaceProfile>();
        }

        if (GUILayout.Button("Add Profile Link"))
        {
            activeController.surfaceProfiles.Add(null);
        }
        for (int p = 0; p < activeController.surfaceProfiles.Count; p++)
        {
            EditorGUILayout.BeginHorizontal();
            activeController.surfaceProfiles[p] = (SurfaceProfile)EditorGUILayout.ObjectField($"Profile {p + 1}", activeController.surfaceProfiles[p], typeof(SurfaceProfile), false);
            if (GUILayout.Button("Remove", GUILayout.Width(70)))
            {
                activeController.surfaceProfiles.RemoveAt(p);
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(activeController);
        }

        EditorGUILayout.EndScrollView();
    }

    // --- Tab 2: Audio Effects Station ---
    private void DrawEffectsStationTab()
    {
        effectsScroll = EditorGUILayout.BeginScrollView(effectsScroll);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Audio Effects & Customization Station", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select a source AudioClip, adjust digital effects in real-time, preview the result, and bake it out into a new Asset.", MessageType.Info);
        GUILayout.Space(10);

        // Source Clip
        EditorGUILayout.BeginHorizontal();
        effectSourceClip = (AudioClip)EditorGUILayout.ObjectField("Source AudioClip", effectSourceClip, typeof(AudioClip), false);
        if (effectSourceClip != null && GUILayout.Button("Play Source", GUILayout.Width(90)))
        {
            PlayClip(effectSourceClip);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        if (effectSourceClip == null)
        {
            EditorGUILayout.HelpBox("Please assign a source audio clip to customize.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        // Render dynamic DSP plugins
        for (int i = 0; i < dspPlugins.Count; i++)
        {
            var plugin = dspPlugins[i];
            EditorGUILayout.BeginVertical("box");
            plugin.Enabled = EditorGUILayout.BeginToggleGroup(plugin.Name, plugin.Enabled);
            
            foreach (var param in plugin.Parameters)
            {
                param.value = EditorGUILayout.Slider(new GUIContent(param.name, param.tooltip), param.value, param.minValue, param.maxValue);
            }
            
            EditorGUILayout.EndToggleGroup();
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        GUILayout.Space(10);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        effectsMasterGain = EditorGUILayout.Slider("Master Gain", effectsMasterGain, 0.1f, 3.0f);
        DrawInfoIcon("Multiply the final processed amplitude before preview or baking. Use > 1.0 to boost quiet samples.");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUILayout.Space(15);

        // Playback Controls
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Preview & Baking Actions", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Preview Effect Settings", GUILayout.Height(30)))
        {
            float[] sampleData = ApplyAudioEffects(effectSourceClip);

            previewProcessedClip = AudioClip.Create($"{effectSourceClip.name}_Preview", sampleData.Length / effectSourceClip.channels, effectSourceClip.channels, effectSourceClip.frequency, false);
            previewProcessedClip.SetData(sampleData, 0);

            // Build amplitude envelope for waveform visualisation
            effectsAmplitudeEnvelope = BuildAmplitudeEnvelope(sampleData, 200);

            PlayClip(previewProcessedClip);
        }

        if (previewProcessedClip != null)
        {
            if (GUILayout.Button("Play Sound", GUILayout.Height(30)))
            {
                PlayClip(previewProcessedClip);
            }
        }

        if (GUILayout.Button("Bake and Save as Asset", GUILayout.Height(30)))
        {
            string cleanName = effectSourceClip.name.Replace(" ", "_");
            string defaultName = $"{cleanName}_FX";
            string folderPath = "Assets/Footsteps/Processed";
            
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            string absoluteFolderPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", folderPath));
            string ext = "wav";
            string assetPath = AssetDatabase.GetAssetPath(effectSourceClip);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string srcExt = Path.GetExtension(assetPath).ToLower().TrimStart('.');
                if (srcExt == "mp3" || srcExt == "ogg") ext = srcExt;
            }

            string filePath = EditorUtility.SaveFilePanel("Save Processed Audio", absoluteFolderPath, defaultName, ext);

            if (!string.IsNullOrEmpty(filePath))
            {
                float[] sampleData = ApplyAudioEffects(effectSourceClip);
                string chosenExt = Path.GetExtension(filePath).ToLower().TrimStart('.');

                if (chosenExt == "wav")
                {
                    WriteWavFile(filePath, sampleData, effectSourceClip.channels, effectSourceClip.frequency);
                }
                else
                {
                    string tempWav = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_temp.wav");
                    WriteWavFile(tempWav, sampleData, effectSourceClip.channels, effectSourceClip.frequency);
                    if (!ConvertFormat(tempWav, filePath))
                    {
                        string fallbackPath = Path.ChangeExtension(filePath, ".wav");
                        File.Move(tempWav, fallbackPath);
                        filePath = fallbackPath;
                        Debug.LogWarning($"ffmpeg conversion to {chosenExt} failed or not installed. saved as wav.");
                    }
                }
                
                AssetDatabase.Refresh();
                
                // Get relative path
                string relativePath = filePath.Substring(filePath.IndexOf("Assets"));
                AudioClip bakedAsset = AssetDatabase.LoadAssetAtPath<AudioClip>(relativePath);

                if (bakedAsset != null)
                {
                    EditorUtility.DisplayDialog("Success", $"Baked and saved custom asset at:\n{relativePath}", "OK");
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        // Waveform preview
        GUILayout.Space(10);
        DrawWaveformEnvelope(effectsAmplitudeEnvelope, "Processed Waveform");

        EditorGUILayout.EndScrollView();
    }

    // --- Tab 3: Settings ---
    private void DrawSettingsTab()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Footstep Designer Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("configure global settings and log warning filters here. all settings default to off (unchecked).", MessageType.Info);
        GUILayout.Space(10);

        bool muteNoProfile = PlayerPrefs.GetInt("FootstepDesigner_MuteNoProfileMatch", 0) == 1;
        bool muteNoClips = PlayerPrefs.GetInt("FootstepDesigner_MuteNoClips", 0) == 1;
        bool muteRaycastMiss = PlayerPrefs.GetInt("FootstepDesigner_MuteRaycastMiss", 0) == 1;

        EditorGUI.BeginChangeCheck();
        
        float oldWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 260f;
        
        muteNoProfile = EditorGUILayout.Toggle("Mute Unmatched Surface Warnings", muteNoProfile);
        muteNoClips = EditorGUILayout.Toggle("Mute Empty Audio Pool Warnings", muteNoClips);
        muteRaycastMiss = EditorGUILayout.Toggle("Mute Raycast Miss Warnings", muteRaycastMiss);
        
        EditorGUIUtility.labelWidth = oldWidth;

        if (EditorGUI.EndChangeCheck())
        {
            PlayerPrefs.SetInt("FootstepDesigner_MuteNoProfileMatch", muteNoProfile ? 1 : 0);
            PlayerPrefs.SetInt("FootstepDesigner_MuteNoClips", muteNoClips ? 1 : 0);
            PlayerPrefs.SetInt("FootstepDesigner_MuteRaycastMiss", muteRaycastMiss ? 1 : 0);
            PlayerPrefs.Save();
        }

        EditorGUILayout.EndVertical();
    }

    // --- Tab 4: Credits & License ---
    private void DrawCreditsTab()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Footstep Designer Suite", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Version 1.1.0 (UPM Compliant)", EditorStyles.miniLabel);
        GUILayout.Space(10);

        EditorGUILayout.LabelField("A native, modular footstep audio design tool providing FMOD-style audio parameters and DSP customization inside Unity.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(10);

        EditorGUILayout.LabelField("Credits:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("- Jaden Adamczak (TCD M.Sc. in Interactive Digital Media - AR/VR, 2026)", EditorStyles.wordWrappedLabel);
        GUILayout.Space(15);

        if (GUILayout.Button("Open GitHub Repository", GUILayout.Height(30)))
        {
            Application.OpenURL("https://github.com/jaden-adamczak/FootstepDesigner");
        }
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("MIT License Text", EditorStyles.boldLabel);
        licenseScroll = EditorGUILayout.BeginScrollView(licenseScroll, GUILayout.Height(200));
        EditorGUILayout.TextArea(MIT_LICENSE_TEXT, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // dsp processing logic
    private float[] ApplyAudioEffects(AudioClip sourceClip)
    {
        int channels = sourceClip.channels;
        int frequency = sourceClip.frequency;
        float[] samples = new float[sourceClip.samples * channels];
        sourceClip.GetData(samples, 0);

        for (int i = 0; i < dspPlugins.Count; i++)
        {
            var plugin = dspPlugins[i];
            if (plugin.Enabled)
            {
                samples = plugin.Apply(samples, channels, frequency);
            }
        }

        // Apply master gain and clamp
        if (Mathf.Abs(effectsMasterGain - 1.0f) > 0.001f)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = Mathf.Clamp(samples[i] * effectsMasterGain, -1f, 1f);
            }
        }

        return samples;
    }

    // --- Helper Methods ---
    private static LayerMask LayerMaskField(string label, LayerMask layerMask)
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
            if (((1 << layerNumbers[i]) & layerMask.value) > 0)
            {
                maskWithoutEmpty |= (1 << i);
            }
        }

        maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers.ToArray());

        int mask = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) > 0)
            {
                mask |= (1 << layerNumbers[i]);
            }
        }
        layerMask.value = mask;
        return layerMask;
    }

    private void DrawInfoIcon(string tooltip)
    {
        GUIContent iconContent = new GUIContent(EditorGUIUtility.IconContent("console.infoIcon").image, tooltip);
        GUILayout.Label(iconContent, GUILayout.Width(16), GUILayout.Height(16));
    }

    private void CreateNewProfile(string tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            EditorUtility.DisplayDialog("Error", "Please enter a valid Surface Tag.", "OK");
            return;
        }

        string cleanTag = tag.Replace(" ", "_");
        string folderDir = "Assets/Footsteps/Profiles";
        if (!Directory.Exists(folderDir))
        {
            Directory.CreateDirectory(folderDir);
        }

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderDir, $"Profile_{cleanTag}.asset"));
        
        SurfaceProfile newProfile = ScriptableObject.CreateInstance<SurfaceProfile>();
        newProfile.surfaceTag = tag;
        newProfile.leftFootBaseSamples = new List<AudioClip>();
        newProfile.rightFootBaseSamples = new List<AudioClip>();
        newProfile.leftFootGranularBakes = new List<AudioClip>();
        newProfile.rightFootGranularBakes = new List<AudioClip>();

        AssetDatabase.CreateAsset(newProfile, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        newProfileTag = "";
        RefreshProfiles();
        selectedProfile = newProfile;
    }

    private void DeleteSelectedProfile()
    {
        if (selectedProfile == null) return;
        string assetPath = AssetDatabase.GetAssetPath(selectedProfile);
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.Refresh();
        selectedProfile = null;
        RefreshProfiles();
    }

    private void RunBaking()
    {
        if (selectedProfile == null) return;

        List<AudioClip> leftBaseClips = selectedProfile.leftFootBaseSamples;
        List<AudioClip> rightBaseClips = selectedProfile.rightFootBaseSamples;

        if (targetFoot == TargetFoot.Left && (leftBaseClips == null || leftBaseClips.Count == 0))
        {
            EditorUtility.DisplayDialog("Error", "No Left Foot base samples found.", "OK");
            return;
        }
        if (targetFoot == TargetFoot.Right && (rightBaseClips == null || rightBaseClips.Count == 0))
        {
            EditorUtility.DisplayDialog("Error", "No Right Foot base samples found.", "OK");
            return;
        }
        if (targetFoot == TargetFoot.Both && (leftBaseClips == null || leftBaseClips.Count == 0) && (rightBaseClips == null || rightBaseClips.Count == 0))
        {
            EditorUtility.DisplayDialog("Error", "No base samples assigned to the selected profile.", "OK");
            return;
        }

        string profilePath = AssetDatabase.GetAssetPath(selectedProfile);
        string profileFolder = Path.GetDirectoryName(profilePath);
        string subFolderName = "Baked_" + selectedProfile.name;
        string outputDir = Path.Combine(profileFolder, subFolderName);
        string absoluteOutputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputDir));

        if (!Directory.Exists(absoluteOutputDir))
        {
            Directory.CreateDirectory(absoluteOutputDir);
        }

        List<BakeTask> tasks = new List<BakeTask>();
        if (targetFoot == TargetFoot.Left || targetFoot == TargetFoot.Both)
        {
            if (leftBaseClips != null)
            {
                for (int clipIdx = 0; clipIdx < leftBaseClips.Count; clipIdx++)
                {
                    AudioClip baseClip = leftBaseClips[clipIdx];
                    if (baseClip == null) continue;
                    for (int i = 1; i <= variationCount; i++)
                    {
                        int globalIndex = clipIdx * variationCount + i;
                        tasks.Add(new BakeTask { sourceClip = baseClip, suffix = "Left", index = globalIndex, isLeft = true });
                    }
                }
            }
        }
        if (targetFoot == TargetFoot.Right || targetFoot == TargetFoot.Both)
        {
            if (rightBaseClips != null)
            {
                for (int clipIdx = 0; clipIdx < rightBaseClips.Count; clipIdx++)
                {
                    AudioClip baseClip = rightBaseClips[clipIdx];
                    if (baseClip == null) continue;
                    for (int i = 1; i <= variationCount; i++)
                    {
                        int globalIndex = clipIdx * variationCount + i;
                        tasks.Add(new BakeTask { sourceClip = baseClip, suffix = "Right", index = globalIndex, isLeft = false });
                    }
                }
            }
        }

        if (tasks.Count == 0) return;

        if (clearExistingBakes)
        {
            if (targetFoot == TargetFoot.Left || targetFoot == TargetFoot.Both)
            {
                selectedProfile.leftFootGranularBakes.Clear();
            }
            if (targetFoot == TargetFoot.Right || targetFoot == TargetFoot.Both)
            {
                selectedProfile.rightFootGranularBakes.Clear();
            }
        }

        try
        {
            for (int t = 0; t < tasks.Count; t++)
            {
                var task = tasks[t];
                EditorUtility.DisplayProgressBar("Baking Granular Variations", 
                    $"Processing {task.suffix} Variation {task.index}/{variationCount}...", 
                    (float)t / tasks.Count);

                string relativeAssetPath = BakeSingleClip(task.sourceClip, task.suffix, task.index, absoluteOutputDir, outputDir);
                if (!string.IsNullOrEmpty(relativeAssetPath))
                {
                    AssetDatabase.ImportAsset(relativeAssetPath, ImportAssetOptions.ForceUpdate);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();

        bool updated = false;
        foreach (var task in tasks)
        {
            string cleanSourceName = task.sourceClip.name.Replace(" ", "_");
            string ext = "wav";
            string assetPath = AssetDatabase.GetAssetPath(task.sourceClip);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string srcExt = Path.GetExtension(assetPath).ToLower().TrimStart('.');
                if (srcExt == "mp3" || srcExt == "ogg") ext = srcExt;
            }

            string fileName = $"{cleanSourceName}_Baked_{task.suffix}_{task.index}.{ext}";
            string relativeAssetPath = Path.Combine(outputDir, fileName).Replace("\\", "/");

            if (ext != "wav" && !File.Exists(Path.Combine(absoluteOutputDir, fileName)))
            {
                fileName = $"{cleanSourceName}_Baked_{task.suffix}_{task.index}.wav";
                relativeAssetPath = Path.Combine(outputDir, fileName).Replace("\\", "/");
            }

            AudioClip bakedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(relativeAssetPath);
            if (bakedClip != null)
            {
                if (task.isLeft)
                {
                    if (!selectedProfile.leftFootGranularBakes.Contains(bakedClip))
                    {
                        selectedProfile.leftFootGranularBakes.Add(bakedClip);
                        updated = true;
                    }
                }
                else
                {
                    if (!selectedProfile.rightFootGranularBakes.Contains(bakedClip))
                    {
                        selectedProfile.rightFootGranularBakes.Add(bakedClip);
                        updated = true;
                    }
                }
            }
        }

        if (updated)
        {
            EditorUtility.SetDirty(selectedProfile);
            AssetDatabase.SaveAssets();
        }

        EditorUtility.DisplayDialog("Success", $"Bake complete! Generated {tasks.Count} variations.", "OK");
    }

    private string BakeSingleClip(AudioClip sourceClip, string suffix, int index, string absoluteOutputDir, string relativeOutputDir)
    {
        int channels = sourceClip.channels;
        int frequency = sourceClip.frequency;
        int totalSamples = sourceClip.samples;

        float[] inputData = new float[totalSamples * channels];
        if (!sourceClip.GetData(inputData, 0))
        {
            Debug.LogError($"Failed to read audio data from {sourceClip.name}");
            return null;
        }

        float devSemitones = Random.Range(-pitchDeviation, pitchDeviation);
        float pitchFactor = Mathf.Pow(2f, devSemitones / 12f);

        float[][] channelOutputs = new float[channels][];
        for (int c = 0; c < channels; c++)
        {
            float[] channelInput = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                channelInput[i] = inputData[i * channels + c];
            }
            channelOutputs[c] = PitchShift(channelInput, frequency, pitchFactor, grainSizeMs, overlapPercent / 100f);
        }

        float[] outputData = new float[totalSamples * channels];
        for (int i = 0; i < totalSamples; i++)
        {
            for (int c = 0; c < channels; c++)
            {
                outputData[i * channels + c] = channelOutputs[c][i];
            }
        }

        // Apply master gain
        if (Mathf.Abs(bakerMasterGain - 1.0f) > 0.001f)
        {
            for (int i = 0; i < outputData.Length; i++)
            {
                outputData[i] = Mathf.Clamp(outputData[i] * bakerMasterGain, -1f, 1f);
            }
        }

        string cleanSourceName = sourceClip.name.Replace(" ", "_");
        string assetPath = AssetDatabase.GetAssetPath(sourceClip);
        string ext = "wav";
        if (!string.IsNullOrEmpty(assetPath))
        {
            string srcExt = Path.GetExtension(assetPath).ToLower().TrimStart('.');
            if (srcExt == "mp3" || srcExt == "ogg") ext = srcExt;
        }

        string fileName = $"{cleanSourceName}_Baked_{suffix}_{index}.{ext}";
        string absoluteFilePath = Path.Combine(absoluteOutputDir, fileName);

        if (ext == "wav")
        {
            WriteWavFile(absoluteFilePath, outputData, channels, frequency);
        }
        else
        {
            string tempWav = Path.Combine(absoluteOutputDir, $"{cleanSourceName}_Baked_{suffix}_{index}_temp.wav");
            WriteWavFile(tempWav, outputData, channels, frequency);
            if (!ConvertFormat(tempWav, absoluteFilePath))
            {
                fileName = $"{cleanSourceName}_Baked_{suffix}_{index}.wav";
                absoluteFilePath = Path.Combine(absoluteOutputDir, fileName);
                File.Move(tempWav, absoluteFilePath);
                Debug.LogWarning($"ffmpeg conversion to {ext} failed or not installed. saved as wav.");
            }
        }

        string relativeAssetPath = Path.Combine(relativeOutputDir, fileName).Replace("\\", "/");
        return relativeAssetPath;
    }

    private static float[] PitchShift(float[] input, int sampleRate, float pitchFactor, float grainSizeMs, float overlapPercent)
    {
        int length = input.Length;
        float[] output = new float[length];
        float[] windowSum = new float[length];

        int grainSize = Mathf.RoundToInt(sampleRate * (grainSizeMs / 1000f));
        if (grainSize < 64) grainSize = 64;
        if (grainSize > length) grainSize = length;

        int hopSize = Mathf.RoundToInt(grainSize * (1f - overlapPercent));
        if (hopSize < 1) hopSize = 1;

        float[] window = new float[grainSize];
        for (int i = 0; i < grainSize; i++)
        {
            window[i] = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * i / (grainSize - 1)));
        }

        for (int outPos = 0; outPos < length; outPos += hopSize)
        {
            for (int i = 0; i < grainSize; i++)
            {
                int outIdx = outPos + i;
                if (outIdx >= length) break;

                float grainCenter = outPos + grainSize / 2f;
                float inputIdx = grainCenter + (i - grainSize / 2f) * pitchFactor;

                float sampleVal = GetInterpolatedSample(input, inputIdx);

                output[outIdx] += sampleVal * window[i];
                windowSum[outIdx] += window[i];
            }
        }

        for (int i = 0; i < length; i++)
        {
            if (windowSum[i] > 1e-5f)
            {
                output[i] /= windowSum[i];
            }
            else
            {
                output[i] = 0f;
            }
        }

        return output;
    }

    private static float GetInterpolatedSample(float[] input, float index)
    {
        int len = input.Length;
        if (index < 0f || index >= len) return 0f;

        int idx0 = (int)index;
        int idx1 = idx0 + 1;
        if (idx1 >= len) return input[idx0];

        float t = index - idx0;
        return Mathf.Lerp(input[idx0], input[idx1], t);
    }

    private static void WriteWavFile(string filePath, float[] samples, int channels, int sampleRate)
    {
        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            using (var binaryWriter = new BinaryWriter(fileStream))
            {
                short bitsPerSample = 16;
                int byteRate = sampleRate * channels * bitsPerSample / 8;
                short blockAlign = (short)(channels * bitsPerSample / 8);
                int subChunk2Size = samples.Length * bitsPerSample / 8;
                int chunkSize = 36 + subChunk2Size;

                binaryWriter.Write(Encoding.UTF8.GetBytes("RIFF"));
                binaryWriter.Write(chunkSize);
                binaryWriter.Write(Encoding.UTF8.GetBytes("WAVE"));

                binaryWriter.Write(Encoding.UTF8.GetBytes("fmt "));
                binaryWriter.Write(16);
                binaryWriter.Write((short)1);
                binaryWriter.Write((short)channels);
                binaryWriter.Write(sampleRate);
                binaryWriter.Write(byteRate);
                binaryWriter.Write(blockAlign);
                binaryWriter.Write(bitsPerSample);

                binaryWriter.Write(Encoding.UTF8.GetBytes("data"));
                binaryWriter.Write(subChunk2Size);

                for (int i = 0; i < samples.Length; i++)
                {
                    float sample = Mathf.Clamp(samples[i], -1f, 1f);
                    short shortSample = (short)(sample * 32767f);
                    binaryWriter.Write(shortSample);
                }
            }
        }
    }

    private static bool ffmpegPromptedThisSession = false;

    public static bool ConvertFormat(string wavPath, string targetPath)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{wavPath}\" \"{targetPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                process.WaitForExit();
                if (process.ExitCode == 0 && File.Exists(targetPath))
                {
                    File.Delete(wavPath);
                    return true;
                }
            }
        }
        catch
        {
        }

        if (!ffmpegPromptedThisSession)
        {
            ffmpegPromptedThisSession = true;
            EditorApplication.delayCall += () =>
            {
                if (EditorUtility.DisplayDialog("FFmpeg Missing",
                    "FFmpeg is required to export MP3 or OGG files. Open download page to install it?",
                    "Open Page", "Cancel"))
                {
                    Application.OpenURL("https://ffmpeg.org/download.html");
                }
            };
        }

        return false;
    }

    private static float[] BuildAmplitudeEnvelope(float[] samples, int width)
    {
        if (samples == null || samples.Length == 0) return null;
        float[] envelope = new float[width];
        int step = Mathf.Max(1, samples.Length / width);
        for (int i = 0; i < width; i++)
        {
            float peak = 0f;
            int start = i * step;
            int end = Mathf.Min(start + step, samples.Length);
            for (int j = start; j < end; j++)
            {
                float a = Mathf.Abs(samples[j]);
                if (a > peak) peak = a;
            }
            envelope[i] = peak;
        }
        return envelope;
    }

    private static void DrawWaveformEnvelope(float[] envelope, string label = "Waveform Amplitude Envelope")
    {
        if (envelope == null || envelope.Length == 0) return;

        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        Rect rect = GUILayoutUtility.GetRect(100, 80, GUILayout.ExpandWidth(true));
        GUI.Box(rect, "");

        float halfH = rect.height / 2f;
        float midY  = rect.y + halfH;
        float stepX = rect.width / envelope.Length;

        Color prev = GUI.color;
        GUI.color = new Color(0.1f, 0.7f, 1f, 0.8f);

        for (int i = 0; i < envelope.Length; i++)
        {
            float h = envelope[i] * halfH;
            float x = rect.x + i * stepX;
            GUI.DrawTexture(new Rect(x, midY - h, Mathf.Max(1f, stepX - 1f), h * 2f), EditorGUIUtility.whiteTexture);
        }

        GUI.color = prev;
    }

    private static void PlayClip(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[FootstepDesigner] PlayClip called with null clip.");
            return;
        }
        EditorAudioPlayer.Play(clip);
    }

    private class BakeTask
    {
        public AudioClip sourceClip;
        public string suffix;
        public int index;
        public bool isLeft;
    }
}
