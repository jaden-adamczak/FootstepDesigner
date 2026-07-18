#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class PerformanceWindow : EditorWindow
{
    private string logsFolder;

    // Stress Test Settings
    private bool showStressTest = false;
    private int charactersToSpawn = 15;
    private int feetPerCharacter = 4; // e.g. Quadrupeds
    private int mockProfilesCount = 10;
    private float stepTriggerRate = 20f; // Steps per second

    private GameObject stressRootGO;
    private List<GameObject> spawnedMockCharacters = new List<GameObject>();
    private List<string> createdProfilePaths = new List<string>();

    private bool isStressLoadTesting = false;
    private double lastStepTime = 0f;

    [MenuItem("Tools/Footsteps/Performance Monitor")]
    public static void ShowWindow()
    {
        GetWindow<PerformanceWindow>("Performance Monitor");
    }

    private void OnEnable()
    {
        logsFolder = Path.Combine(Application.dataPath, "../Logs");
    }

    private void OnDisable()
    {
        StopStressLoadTest();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Footstep Performance Diagnostics", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Monitor real-time sound system performance, variety statistics, and trigger automated load stress tests.", MessageType.Info);
        GUILayout.Space(10);

        PerformanceTracker tracker = FindAnyObjectByType<PerformanceTracker>();

        if (tracker == null)
        {
            EditorGUILayout.HelpBox("Performance tracker not found in the active scene. Play a scene with a PerformanceTracker component.", MessageType.Warning);
            if (GUILayout.Button("Create Tracker GameObject in Scene"))
            {
                GameObject go = new GameObject("PerformanceTracker");
                go.AddComponent<PerformanceTracker>();
                Selection.activeGameObject = go;
            }
            return;
        }

        // Live Audio System Diagnostics
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Voice & Engine Metrics", EditorStyles.boldLabel);
        GUILayout.Space(5);

        float fps = tracker.GetCurrentFps();
        float heap = tracker.GetHeapSizeMb();
        int steps = tracker.GetTotalStepsCount();
        int foley = tracker.GetTotalFoleyCount();
        int activeVoices = tracker.GetActiveVoicesCount();
        int maxVoices = tracker.GetMaxRealVoices();

        // FPS Bar
        EditorGUILayout.LabelField($"System Frame Rate: {fps:F1} FPS");
        Rect rectFps = GUILayoutUtility.GetRect(100, 15, GUILayout.ExpandWidth(true));
        float fpsPercentage = Mathf.Clamp01(fps / 120f);
        EditorGUI.ProgressBar(rectFps, fpsPercentage, $"{fps:F1} FPS");
        GUILayout.Space(5);

        // Memory Bar
        EditorGUILayout.LabelField($"GC Allocated Heap: {heap:F2} MB");
        Rect rectHeap = GUILayoutUtility.GetRect(100, 15, GUILayout.ExpandWidth(true));
        float heapPercentage = Mathf.Clamp01(heap / 256f);
        EditorGUI.ProgressBar(rectHeap, heapPercentage, $"{heap:F2} MB");
        GUILayout.Space(5);

        // Voice Count Bar
        EditorGUILayout.LabelField($"Active Playing Voices: {activeVoices} / {maxVoices} (Unity limit)");
        Rect rectVoices = GUILayoutUtility.GetRect(100, 15, GUILayout.ExpandWidth(true));
        float voicePercentage = maxVoices > 0 ? Mathf.Clamp01((float)activeVoices / maxVoices) : 0f;
        EditorGUI.ProgressBar(rectVoices, voicePercentage, $"{activeVoices} / {maxVoices} Voices");
        
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // Repetition & Variety Metrics
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Auditory Variety & Repetition", EditorStyles.boldLabel);
        GUILayout.Space(5);

        float varietyIndex = tracker.GetVarietyIndex();
        int repetitions = tracker.GetConsecutiveRepetitionsCount();

        EditorGUILayout.LabelField($"Consecutive Repetitions: {repetitions} (Same clip triggered back-to-back)");
        EditorGUILayout.LabelField($"Variety Index (Last 20 steps): {varietyIndex:P1}");
        
        Rect rectVariety = GUILayoutUtility.GetRect(100, 15, GUILayout.ExpandWidth(true));
        EditorGUI.ProgressBar(rectVariety, varietyIndex, $"Variety Index: {varietyIndex:P0}");

        if (varietyIndex < 0.3f && steps > 5)
        {
            EditorGUILayout.HelpBox("Warning: High audio repetition detected! Consider baking more granular variations or adding more base clips to your active Surface Profiles.", MessageType.Warning);
        }

        EditorGUILayout.LabelField($"Total Steps logged: {steps}");
        EditorGUILayout.LabelField($"Total Foley/Accessory logged: {foley}");

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // Dynamic Scatter Plot
        EditorGUILayout.BeginVertical("box");
        DrawScatterPlot(tracker.GetStepHistory());
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // Stress Testing Foldout
        showStressTest = EditorGUILayout.Foldout(showStressTest, "Scene Stress & Automated Load Spawner");
        if (showStressTest)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.HelpBox("Instantly duplicate controllers, legs, and dynamic profiles to benchmark audio rendering overhead.", MessageType.None);

            charactersToSpawn = EditorGUILayout.IntField("Mock Characters", charactersToSpawn);
            feetPerCharacter = EditorGUILayout.IntSlider("Feet per Character", feetPerCharacter, 1, 16);
            mockProfilesCount = EditorGUILayout.IntField("Dynamic Profiles", mockProfilesCount);

            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Stress Scene", GUILayout.Height(25)))
            {
                GenerateStressScene();
            }
            if (GUILayout.Button("Clear Stress Scene", GUILayout.Height(25)))
            {
                ClearStressScene();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            if (isStressLoadTesting)
            {
                GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
                if (GUILayout.Button("Stop Load Test", GUILayout.Height(30)))
                {
                    StopStressLoadTest();
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
                if (GUILayout.Button("Start Auto-Step Load Test", GUILayout.Height(30)))
                {
                    StartStressLoadTest();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        GUILayout.Space(15);
        EditorGUILayout.LabelField("Log Management", EditorStyles.boldLabel);

        // Show active log path if tracker is running
        string activeLogPath = tracker != null ? tracker.GetLogPath() : null;
        if (!string.IsNullOrEmpty(activeLogPath))
        {
            EditorGUILayout.LabelField("Active Log:", Path.GetFileName(activeLogPath), EditorStyles.miniLabel);
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Open Active Log", GUILayout.Height(25)))
        {
            if (!string.IsNullOrEmpty(activeLogPath) && File.Exists(activeLogPath))
            {
                EditorUtility.OpenWithDefaultApp(activeLogPath);
            }
            else
            {
                EditorUtility.DisplayDialog("Log Missing", "No active log file. Enter Play Mode with a PerformanceTracker to start logging.", "OK");
            }
        }

        if (GUILayout.Button("Open Logs Folder", GUILayout.Height(25)))
        {
            if (Directory.Exists(logsFolder))
            {
                EditorUtility.RevealInFinder(logsFolder);
            }
            else
            {
                EditorUtility.DisplayDialog("No Logs", "Logs folder does not exist yet. Run a scene to generate tracking data.", "OK");
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawScatterPlot(List<PerformanceTracker.StepRecord> history)
    {
        if (history == null || history.Count == 0)
        {
            EditorGUILayout.HelpBox("No step history captured yet. Walk or trigger steps in Play mode to view distribution.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Velocity Curves Scatter Plot (Last 100 Events)", EditorStyles.boldLabel);
        Rect rect = GUILayoutUtility.GetRect(150, 120, GUILayout.ExpandWidth(true));
        GUI.Box(rect, "");

        Color oldColor = GUI.color;

        // Draw grid
        Handles.BeginGUI();
        Handles.color = new Color(0.2f, 0.2f, 0.2f, 0.3f);
        Handles.DrawLine(new Vector2(rect.x, rect.y + rect.height * 0.5f), new Vector2(rect.x + rect.width, rect.y + rect.height * 0.5f));
        Handles.DrawLine(new Vector2(rect.x + rect.width * 0.5f, rect.y), new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height));
        
        // Find maximum speed in history to scale X axis, default to 6.0f
        float maxSpeed = 6f;
        foreach (var r in history)
        {
            if (r.speed > maxSpeed) maxSpeed = r.speed;
        }

        // Draw data points
        foreach (var step in history)
        {
            float normX = Mathf.Clamp01(step.speed / maxSpeed);
            float x = rect.x + normX * rect.width;

            // Volume (cyan dots)
            float normYVol = 1f - Mathf.Clamp01(step.volume);
            float yVol = rect.y + normYVol * rect.height;

            // Pitch (orange dots, range 0.5 to 1.5)
            float normYPitch = 1f - Mathf.Clamp01((step.pitch - 0.5f) / 1.0f);
            float yPitch = rect.y + normYPitch * rect.height;

            // Draw volume dot
            GUI.color = step.isConsecutiveRepetition ? new Color(1f, 0.2f, 0.2f, 0.8f) : new Color(0f, 0.8f, 1f, 0.7f);
            GUI.DrawTexture(new Rect(x - 2f, yVol - 2f, 4f, 4f), EditorGUIUtility.whiteTexture);

            // Draw pitch dot (only for footstep sounds, skipping foley to prevent clutter)
            if (!step.isFoley)
            {
                GUI.color = new Color(1f, 0.6f, 0.1f, 0.7f);
                GUI.DrawTexture(new Rect(x - 2f, yPitch - 2f, 4f, 4f), EditorGUIUtility.whiteTexture);
            }
        }

        // Draw Y-axis reference units directly on the graph background
        GUI.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, 150, 15), "1.0 (Vol) / 1.5 (Pitch)", EditorStyles.miniLabel);
        GUI.Label(new Rect(rect.x + 4f, rect.y + rect.height * 0.5f - 8f, 150, 15), "0.5 (Vol) / 1.0 (Pitch)", EditorStyles.miniLabel);
        GUI.Label(new Rect(rect.x + 4f, rect.y + rect.height - 15f, 150, 15), "0.0 (Vol) / 0.5 (Pitch)", EditorStyles.miniLabel);

        Handles.EndGUI();
        GUI.color = oldColor;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Legend: ", EditorStyles.miniLabel);
        GUI.color = new Color(0f, 0.8f, 1f, 1f);
        GUILayout.Label("■ Volume (0.0 - 1.0)  ", EditorStyles.miniLabel);
        GUI.color = new Color(1f, 0.6f, 0.1f, 1f);
        GUILayout.Label("■ Pitch (0.5 - 1.5)  ", EditorStyles.miniLabel);
        GUI.color = new Color(1f, 0.2f, 0.2f, 1f);
        GUILayout.Label("■ Repetition Warning  ", EditorStyles.miniLabel);
        GUI.color = Color.white;
        GUILayout.Label($"X-Axis: Speed (0 to {maxSpeed:F1} m/s)", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private void GenerateStressScene()
    {
        // 1. Clean any old stress objects first to avoid directory locks
        ClearStressScene();

        // 2. Setup folders safely using AssetDatabase (forces Unity to register directories)
        string parentFolder = "Assets/LocalSynth";
        string subFolder = "StressTest";
        string stressTestFolder = $"{parentFolder}/{subFolder}";

        if (!AssetDatabase.IsValidFolder(parentFolder))
        {
            AssetDatabase.CreateFolder("Assets", "LocalSynth");
        }
        if (!AssetDatabase.IsValidFolder(stressTestFolder))
        {
            AssetDatabase.CreateFolder(parentFolder, subFolder);
        }

        stressRootGO = new GameObject("__STRESS_TEST_ROOT__");

        // 2. Generate dynamic profiles
        // We will look for an existing SurfaceProfile to copy, or create mock profiles
        string[] existingProfiles = AssetDatabase.FindAssets("t:SurfaceProfile");
        SurfaceProfile sourceProfile = null;
        if (existingProfiles.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(existingProfiles[0]);
            sourceProfile = AssetDatabase.LoadAssetAtPath<SurfaceProfile>(path);
        }

        List<SurfaceProfile> mockProfiles = new List<SurfaceProfile>();

        for (int p = 0; p < mockProfilesCount; p++)
        {
            SurfaceProfile newProfile = ScriptableObject.CreateInstance<SurfaceProfile>();
            newProfile.surfaceTag = $"Stress_Surface_{p}";

            if (sourceProfile != null)
            {
                // Replicate audio lists from the template profile
                newProfile.leftFootBaseSamples = new List<AudioClip>(sourceProfile.leftFootBaseSamples);
                newProfile.leftFootGranularBakes = new List<AudioClip>(sourceProfile.leftFootGranularBakes);
                newProfile.rightFootBaseSamples = new List<AudioClip>(sourceProfile.rightFootBaseSamples);
                newProfile.rightFootGranularBakes = new List<AudioClip>(sourceProfile.rightFootGranularBakes);
                newProfile.leftVolumeRandomness = sourceProfile.leftVolumeRandomness;
                newProfile.leftPitchRandomness = sourceProfile.leftPitchRandomness;
                newProfile.rightVolumeRandomness = sourceProfile.rightVolumeRandomness;
                newProfile.rightPitchRandomness = sourceProfile.rightPitchRandomness;
            }

            string assetPath = $"{stressTestFolder}/Stress_Profile_{p}.asset";
            AssetDatabase.CreateAsset(newProfile, assetPath);
            createdProfilePaths.Add(assetPath);
            mockProfiles.Add(newProfile);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3. Spawn Mock Character GameObjects
        for (int c = 0; c < charactersToSpawn; c++)
        {
            GameObject mockChar = new GameObject($"Mock_Character_{c}");
            mockChar.transform.SetParent(stressRootGO.transform);
            
            // Add controller
            DualFootstepController controller = mockChar.AddComponent<DualFootstepController>();
            controller.autoDetectSteps = false; // Trigger manually to simulate load
            controller.fallbackToFirstProfile = true; // Avoid tag matching issues in empty scenes
            controller.surfaceProfiles = new List<SurfaceProfile>(mockProfiles);


            // Add feet bones
            controller.feet = new List<DualFootstepController.FootSetup>();
            for (int f = 0; f < feetPerCharacter; f++)
            {
                GameObject footGO = new GameObject($"Foot_{f}");
                footGO.transform.SetParent(mockChar.transform);
                footGO.transform.localPosition = new Vector3((f % 2 == 0 ? -0.2f : 0.2f), 0f, (f > 1 ? -0.5f : 0f));

                AudioSource source = footGO.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1.0f; // 3D

                var footSetup = new DualFootstepController.FootSetup
                {
                    name = $"Leg_{f}",
                    footBone = footGO.transform,
                    audioSource = source,
                    isLeft = (f % 2 == 0)
                };
                controller.feet.Add(footSetup);
            }

            spawnedMockCharacters.Add(mockChar);
        }

        Debug.Log($"[StressTest] Generated {charactersToSpawn} characters with {feetPerCharacter} legs each, mapped to {mockProfilesCount} mock profiles.");
    }

    private void ClearStressScene()
    {
        StopStressLoadTest();

        // Destroy spawned characters
        if (stressRootGO != null)
        {
            DestroyImmediate(stressRootGO);
            stressRootGO = null;
        }

        // Find root GO if named __STRESS_TEST_ROOT__ and not reference-tracked
        GameObject root = GameObject.Find("__STRESS_TEST_ROOT__");
        if (root != null)
        {
            DestroyImmediate(root);
        }

        spawnedMockCharacters.Clear();

        // Clean up temporary assets
        if (createdProfilePaths.Count > 0)
        {
            foreach (var path in createdProfilePaths)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
            createdProfilePaths.Clear();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Clean StressTest folder if empty
        string stressTestFolder = "Assets/LocalSynth/StressTest";
        if (Directory.Exists(stressTestFolder))
        {
            string[] files = Directory.GetFiles(stressTestFolder);
            if (files.Length == 0)
            {
                Directory.Delete(stressTestFolder);
                string metaPath = stressTestFolder + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
                AssetDatabase.Refresh();
            }
        }
    }

    private void StartStressLoadTest()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Simulation Warning", "Please enter Play Mode in Unity before starting the Load Test.", "OK");
            return;
        }

        if (spawnedMockCharacters.Count == 0)
        {
            EditorUtility.DisplayDialog("Setup Error", "No stress characters spawned. Click 'Generate Stress Scene' first.", "OK");
            return;
        }

        isStressLoadTesting = true;
        foreach (var mockChar in spawnedMockCharacters)
        {
            if (mockChar != null)
            {
                var walker = mockChar.GetComponent<MockCharacterWalker>();
                if (walker == null) walker = mockChar.AddComponent<MockCharacterWalker>();
                walker.enabled = true;
            }
        }
        Debug.Log("[StressTest] Load testing started.");
    }

    private void StopStressLoadTest()
    {
        if (isStressLoadTesting)
        {
            isStressLoadTesting = false;
            foreach (var mockChar in spawnedMockCharacters)
            {
                if (mockChar != null)
                {
                    var walker = mockChar.GetComponent<MockCharacterWalker>();
                    if (walker != null) walker.enabled = false;
                }
            }
            Debug.Log("[StressTest] Load testing stopped.");
        }
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            Repaint();
        }
    }
}
#endif
