#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public class PerformanceWindow : EditorWindow
{
    private string logPath;

    [MenuItem("Tools/Footsteps/Performance Monitor")]
    public static void ShowWindow()
    {
        GetWindow<PerformanceWindow>("Performance Monitor");
    }

    private void OnEnable()
    {
        logPath = Path.Combine(Application.dataPath, "../footstep_performance.log");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Footstep Performance Diagnostics", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("monitor real time sound system performance and tracking data.", MessageType.Info);
        GUILayout.Space(10);

        PerformanceTracker tracker = FindAnyObjectByType<PerformanceTracker>();

        if (tracker == null)
        {
            EditorGUILayout.HelpBox("performance tracker not found in active scene. ensure a game is running with a performance tracker component.", MessageType.Warning);
            if (GUILayout.Button("Create Tracker GameObject in Scene"))
            {
                GameObject go = new GameObject("PerformanceTracker");
                go.AddComponent<PerformanceTracker>();
                Selection.activeGameObject = go;
            }
            return;
        }

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Live Metrics", EditorStyles.boldLabel);
        GUILayout.Space(5);

        float fps = tracker.GetCurrentFps();
        float heap = tracker.GetHeapSizeMb();
        int steps = tracker.GetTotalStepsCount();
        int audioSourcesCount = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length;

        EditorGUILayout.LabelField($"Current FPS: {fps:F1}");
        Rect rectFps = GUILayoutUtility.GetRect(100, 15, GUILayout.ExpandWidth(true));
        float fpsPercentage = Mathf.Clamp01(fps / 120f);
        EditorGUI.ProgressBar(rectFps, fpsPercentage, $"{fps:F1} FPS");

        GUILayout.Space(5);
        EditorGUILayout.LabelField($"GC Allocated Heap: {heap:F2} MB");
        Rect rectHeap = GUILayoutUtility.GetRect(100, 15, GUILayout.ExpandWidth(true));
        float heapPercentage = Mathf.Clamp01(heap / 256f);
        EditorGUI.ProgressBar(rectHeap, heapPercentage, $"{heap:F2} MB");

        GUILayout.Space(5);
        EditorGUILayout.LabelField($"Active Audio Sources: {audioSourcesCount}");
        EditorGUILayout.LabelField($"Total Footstep Events: {steps}");

        EditorGUILayout.EndVertical();

        GUILayout.Space(15);
        EditorGUILayout.LabelField("Log Management", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Open Log File", GUILayout.Height(25)))
        {
            if (File.Exists(logPath))
            {
                EditorUtility.OpenWithDefaultApp(logPath);
            }
            else
            {
                EditorUtility.DisplayDialog("Log Missing", "log file does not exist yet. run a scene to generate tracking data.", "ok");
            }
        }

        if (GUILayout.Button("Clear Log File", GUILayout.Height(25)))
        {
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
                File.WriteAllText(logPath, "footstep performance log initialized\ntime,fps,frametime_ms,heap_mb,audio_sources,step_count\n");
                EditorUtility.DisplayDialog("Log Cleared", "performance tracking log has been reset.", "ok");
            }
        }

        EditorGUILayout.EndHorizontal();
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
