using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class FootstepDesignerWindow : EditorWindow
{
    private int activeTab = 0;
    private string[] tabNames = { "Profiles", "Scene Controller", "Audio Effects Station", "Credits & License" };

    // Profiles Tab variables
    private List<SurfaceProfile> allProfiles = new List<SurfaceProfile>();
    private SurfaceProfile selectedProfile;
    private Vector2 sidebarScroll;
    private Vector2 detailsScroll;
    private string searchString = "";
    private string newProfileTag = "";

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

        // Left Sidebar
        EditorGUILayout.BeginVertical("box", GUILayout.Width(250), GUILayout.ExpandHeight(true));
        EditorGUILayout.LabelField("Surface Profiles", EditorStyles.boldLabel);
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

        // Create Profile Block
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Create New Profile", EditorStyles.boldLabel);
        newProfileTag = EditorGUILayout.TextField("Surface Tag", newProfileTag);
        if (GUILayout.Button("Create Profile"))
        {
            CreateNewProfile(newProfileTag);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndVertical();

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

            EditorGUI.BeginChangeCheck();

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
            variationCount = EditorGUILayout.IntSlider("Variations Count", variationCount, 1, 20);
            DrawInfoIcon("Number of unique WAV variations to generate based on the first Base AudioClip in your pool.");
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
            DrawInfoIcon("If enabled, plays synthesized granular variations from the profile. If disabled, plays random base clips with on-the-spot pitch/volume shifting.");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            activeController.spatialBlend = EditorGUILayout.Slider("Spatial Blend (2D/3D)", activeController.spatialBlend, 0f, 1f);
            DrawInfoIcon("0 = Stereo 2D. 1 = Spatialized 3D (highly recommended for VR).");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            activeController.defaultMixerGroup = (UnityEngine.Audio.AudioMixerGroup)EditorGUILayout.ObjectField("Default Mixer Group", activeController.defaultMixerGroup, typeof(UnityEngine.Audio.AudioMixerGroup), false);
            DrawInfoIcon("Default Audio Mixer Group destination.");
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

        GUILayout.Space(15);

        // Playback Controls
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Preview & Baking Actions", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Preview Effect Settings", GUILayout.Height(30)))
        {
            float[] sampleData = ApplyAudioEffects(effectSourceClip);
            
            // Create a preview clip
            previewProcessedClip = AudioClip.Create($"{effectSourceClip.name}_Preview", sampleData.Length / effectSourceClip.channels, effectSourceClip.channels, effectSourceClip.frequency, false);
            previewProcessedClip.SetData(sampleData, 0);

            PlayClip(previewProcessedClip);
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
            string filePath = EditorUtility.SaveFilePanel("Save Processed Audio", absoluteFolderPath, defaultName, "wav");

            if (!string.IsNullOrEmpty(filePath))
            {
                float[] sampleData = ApplyAudioEffects(effectSourceClip);
                
                WriteWavFile(filePath, sampleData, effectSourceClip.channels, effectSourceClip.frequency);
                
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
        EditorGUILayout.EndScrollView();
    }

    // --- Tab 3: Credits & License ---
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

        AudioClip leftBase = selectedProfile.leftFootBaseSamples.Count > 0 ? selectedProfile.leftFootBaseSamples[0] : null;
        AudioClip rightBase = selectedProfile.rightFootBaseSamples.Count > 0 ? selectedProfile.rightFootBaseSamples[0] : null;

        if (targetFoot == TargetFoot.Left && leftBase == null)
        {
            EditorUtility.DisplayDialog("Error", "No Left Foot base sample found.", "OK");
            return;
        }
        if (targetFoot == TargetFoot.Right && rightBase == null)
        {
            EditorUtility.DisplayDialog("Error", "No Right Foot base sample found.", "OK");
            return;
        }
        if (targetFoot == TargetFoot.Both && leftBase == null && rightBase == null)
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
            if (leftBase != null)
            {
                for (int i = 1; i <= variationCount; i++)
                {
                    tasks.Add(new BakeTask { sourceClip = leftBase, suffix = "Left", index = i, isLeft = true });
                }
            }
        }
        if (targetFoot == TargetFoot.Right || targetFoot == TargetFoot.Both)
        {
            if (rightBase != null)
            {
                for (int i = 1; i <= variationCount; i++)
                {
                    tasks.Add(new BakeTask { sourceClip = rightBase, suffix = "Right", index = i, isLeft = false });
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
            string fileName = $"{cleanSourceName}_Baked_{task.suffix}_{task.index}.wav";
            string relativeAssetPath = Path.Combine(outputDir, fileName).Replace("\\", "/");

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

        string cleanSourceName = sourceClip.name.Replace(" ", "_");
        string fileName = $"{cleanSourceName}_Baked_{suffix}_{index}.wav";
        string absoluteFilePath = Path.Combine(absoluteOutputDir, fileName);

        WriteWavFile(absoluteFilePath, outputData, channels, frequency);

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

    private static void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        try
        {
            System.Reflection.Assembly assembly = typeof(AudioImporter).Assembly;
            System.Type type = assembly.GetType("UnityEditor.AudioUtil");
            System.Reflection.MethodInfo method = type.GetMethod("PlayPreviewClip", 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (method != null)
            {
                method.Invoke(null, new object[] { clip, 0, false });
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Failed to preview clip via reflection: " + e.Message);
        }
    }

    private class BakeTask
    {
        public AudioClip sourceClip;
        public string suffix;
        public int index;
        public bool isLeft;
    }
}
