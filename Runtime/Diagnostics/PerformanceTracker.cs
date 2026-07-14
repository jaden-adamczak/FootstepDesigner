using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class PerformanceTracker : MonoBehaviour
{
    public static PerformanceTracker Instance { get; private set; }

    [SerializeField] private float logInterval = 1f;
    [SerializeField] private bool enableTracking = true;

    private string logPath;
    private float timer;
    private int totalSteps;
    private int totalFoleyEvents;
    private float fpsTimer;
    private int fpsAccumulator;
    private float currentFps;

    // Telemetry & Diagnostic Buffers
    public struct StepRecord
    {
        public float time;
        public float speed;
        public float volume;
        public float pitch;
        public string surface;
        public string clipName;
        public bool isFoley;
        public bool isConsecutiveRepetition;
    }

    private List<StepRecord> stepHistory = new List<StepRecord>();
    private const int MaxHistoryCount = 100;

    // Variety Tracking
    private string lastLeftClip = "";
    private string lastRightClip = "";
    private string lastFoleyClip = "";
    private Queue<string> recentClips = new Queue<string>();
    private const int VarietyWindowSize = 20;

    // Diagnostics Stats
    private int consecutiveRepetitionsCount = 0;
    private int activeVoicesCount = 0;
    private int maxRealVoices = 32;

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
        maxRealVoices = AudioSettings.GetConfiguration().numRealVoices;
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
                    writer.WriteLine("time,fps,frametime_ms,heap_mb,active_voices,max_voices,audio_sources,step_count,foley_count");
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
        
        // Count active voices playing currently
        int audioSourcesCount = 0;
        activeVoicesCount = 0;
        AudioSource[] allSources = FindObjectsByType<AudioSource>();
        audioSourcesCount = allSources.Length;
        for (int i = 0; i < allSources.Length; i++)
        {
            if (allSources[i] != null && allSources[i].isPlaying)
            {
                activeVoicesCount++;
            }
        }

        string logLine = string.Format("{0:F2},{1:F1},{2:F2},{3:F2},{4},{5},{6},{7},{8}",
            Time.time,
            currentFps,
            frameTime,
            heapMb,
            activeVoicesCount,
            maxRealVoices,
            audioSourcesCount,
            totalSteps,
            totalFoleyEvents
        );

        WriteLogLine(logLine);
    }

    public void LogStepEvent(bool isLeft, float speed, string surfaceTag, string clipName, float volume, float pitch, bool isFoley)
    {
        if (isFoley) totalFoleyEvents++;
        else totalSteps++;

        if (!enableTracking) return;

        // Check repetition
        bool isRepetition = false;
        if (isFoley)
        {
            isRepetition = (clipName == lastFoleyClip);
            lastFoleyClip = clipName;
        }
        else
        {
            if (isLeft)
            {
                isRepetition = (clipName == lastLeftClip);
                lastLeftClip = clipName;
            }
            else
            {
                isRepetition = (clipName == lastRightClip);
                lastRightClip = clipName;
            }
        }

        if (isRepetition) consecutiveRepetitionsCount++;

        // Add to variety queue
        recentClips.Enqueue(clipName);
        if (recentClips.Count > VarietyWindowSize)
        {
            recentClips.Dequeue();
        }

        // Add to Circular step history
        StepRecord record = new StepRecord
        {
            time = Time.time,
            speed = speed,
            volume = volume,
            pitch = pitch,
            surface = surfaceTag,
            clipName = clipName,
            isFoley = isFoley,
            isConsecutiveRepetition = isRepetition
        };

        stepHistory.Add(record);
        if (stepHistory.Count > MaxHistoryCount)
        {
            stepHistory.RemoveAt(0);
        }

        string side = isFoley ? "foley" : (isLeft ? "left" : "right");
        string logLine = string.Format("{0:F2},[step],{1},speed={2:F2},surface={3},clip={4},volume={5:F3},pitch={6:F3},rep={7}",
            Time.time,
            side,
            speed,
            surfaceTag,
            clipName,
            volume,
            pitch,
            isRepetition ? "1" : "0"
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
        catch (System.Exception)
        {
            // Fail silently or to console if editor isn't in play mode
        }
    }

    public float GetCurrentFps() => currentFps;
    public float GetHeapSizeMb() => System.GC.GetTotalMemory(false) / (1024f * 1024f);
    public int GetTotalStepsCount() => totalSteps;
    public int GetTotalFoleyCount() => totalFoleyEvents;
    public int GetActiveVoicesCount() => activeVoicesCount;
    public int GetMaxRealVoices() => maxRealVoices;
    public int GetConsecutiveRepetitionsCount() => consecutiveRepetitionsCount;
    public List<StepRecord> GetStepHistory() => stepHistory;

    public float GetVarietyIndex()
    {
        if (recentClips.Count == 0) return 1f;
        HashSet<string> unique = new HashSet<string>(recentClips);
        return (float)unique.Count / recentClips.Count;
    }
}
