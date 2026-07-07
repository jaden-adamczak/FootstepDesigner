using UnityEngine;
using System.IO;
using System.Text;

public class PerformanceTracker : MonoBehaviour
{
    public static PerformanceTracker Instance { get; private set; }

    [SerializeField] private float logInterval = 1f;
    [SerializeField] private bool enableTracking = true;

    private string logPath;
    private float timer;
    private int totalSteps;
    private float frameCount;
    private float fpsTimer;
    private int fpsAccumulator;
    private float currentFps;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        logPath = Path.Combine(Application.dataPath, "../footstep_performance.log");
        InitializeLogFile();
    }

    private void InitializeLogFile()
    {
        try
        {
            using (var stream = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.WriteLine("footstep performance log initialized");
                    writer.WriteLine("time,fps,frametime_ms,heap_mb,audio_sources,step_count");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("failed to initialize performance log: " + e.Message);
        }
    }

    private void Update()
    {
        if (!enableTracking) return;

        fpsAccumulator++;
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer >= 1f)
        {
            currentFps = fpsAccumulator / fpsTimer;
            fpsAccumulator = 0;
            fpsTimer = 0f;
        }

        timer += Time.deltaTime;
        if (timer >= logInterval)
        {
            timer = 0f;
            LogPeriodicMetrics();
        }
    }

    private void LogPeriodicMetrics()
    {
        float frameTime = Time.unscaledDeltaTime * 1000f;
        long heapSize = System.GC.GetTotalMemory(false);
        float heapMb = heapSize / (1024f * 1024f);
        int audioSourcesCount = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length;

        string logLine = string.Format("{0:F2},{1:F1},{2:F2},{3:F2},{4},{5}",
            Time.time,
            currentFps,
            frameTime,
            heapMb,
            audioSourcesCount,
            totalSteps
        );

        WriteLogLine(logLine);
    }

    public void LogStepEvent(bool isLeft, float speed, string surfaceTag)
    {
        totalSteps++;
        if (!enableTracking) return;

        string side = isLeft ? "left" : "right";
        string logLine = string.Format("{0:F2},[step],{1},speed={2:F2},surface={3}",
            Time.time,
            side,
            speed,
            surfaceTag
        );

        WriteLogLine(logLine);
    }

    private void WriteLogLine(string line)
    {
        try
        {
            using (var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.WriteLine(line);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("failed to write performance log line: " + e.Message);
        }
    }

    public float GetCurrentFps() => currentFps;
    public float GetHeapSizeMb() => System.GC.GetTotalMemory(false) / (1024f * 1024f);
    public int GetTotalStepsCount() => totalSteps;
}
