using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class PerformanceTracker : MonoBehaviour
{
    public static PerformanceTracker Instance { get; private set; }

    public enum TriggerMode
    {
        FootstepDesigner,
        TraditionalSimulation
    }

    [SerializeField] private float logInterval = 1f;
    [SerializeField] private bool enableTracking = true;
    [SerializeField] private string logLabel = "";
    [SerializeField] private float trackingDuration = 0f; // 0 = infinite tracking
    [SerializeField] private bool autoQuitOnFinish = false;
    [SerializeField] private TriggerMode activeTriggerMode = TriggerMode.FootstepDesigner;

    private string logPath;
    private float timer;
    private float trackingTimer;
    private bool isTrackingFinished;
    private string activeModeLabel = "Designer";
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
        public string characterName;
        public string footName;
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
            activeModeLabel = activeTriggerMode.ToString();
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        string logsFolder = Path.Combine(Application.dataPath, "../Logs");
        if (!Directory.Exists(logsFolder))
        {
            Directory.CreateDirectory(logsFolder);
        }

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string labelSuffix = string.IsNullOrEmpty(logLabel) ? "" : $"_{logLabel}";
        string fileName = $"footstep_perf_{timestamp}{labelSuffix}.log";
        logPath = Path.Combine(logsFolder, fileName);
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
                    writer.WriteLine("# footstep performance log");
                    writer.WriteLine($"# date={System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"# unity={Application.unityVersion}");
                    writer.WriteLine($"# platform={Application.platform}");
                    writer.WriteLine($"# mode={activeModeLabel}");
                    writer.WriteLine($"# duration={trackingDuration.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}s");
                    writer.WriteLine($"# log_interval={logInterval.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}s");
                    writer.WriteLine($"# label={logLabel}");
                    writer.WriteLine($"# max_real_voices={AudioSettings.GetConfiguration().numRealVoices}");
                    writer.WriteLine($"# sample_rate={AudioSettings.outputSampleRate}");

                    // Count scene characters and feet
                    DualFootstepController[] controllers = FindObjectsByType<DualFootstepController>();
                    int totalFeet = 0;
                    int totalProfiles = 0;
                    string triggerMode = "Unknown";
                    foreach (var c in controllers)
                    {
                        if (c.feet != null) totalFeet += c.feet.Count;
                        if (c.surfaceProfiles != null && c.surfaceProfiles.Count > totalProfiles)
                            totalProfiles = c.surfaceProfiles.Count;
                        triggerMode = c.triggerMode.ToString();
                    }
                    writer.WriteLine($"# characters={controllers.Length}");
                    writer.WriteLine($"# total_feet={totalFeet}");
                    writer.WriteLine($"# surface_profiles={totalProfiles}");
                    writer.WriteLine($"# trigger_mode={triggerMode}");

                    writer.WriteLine("#");
                    writer.WriteLine("time,mode,fps,frametime_ms,heap_mb,active_voices,max_voices,audio_sources,step_count,foley_count");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("failed to initialize performance log: " + e.Message);
        }
    }

    public string ActiveModeLabel
    {
        get => activeModeLabel;
        set => activeModeLabel = value;
    }

    public TriggerMode ActiveTriggerMode
    {
        get => activeTriggerMode;
        set 
        {
            activeTriggerMode = value;
            activeModeLabel = value.ToString();
        }
    }

    private void Update()
    {
        if (!enableTracking || isTrackingFinished) return;

        fpsAccumulator++;
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer >= 1f)
        {
            currentFps = fpsAccumulator / fpsTimer;
            fpsAccumulator = 0;
            fpsTimer = 0f;
        }

        if (trackingDuration > 0f)
        {
            trackingTimer += Time.deltaTime;
            if (trackingTimer >= trackingDuration)
            {
                isTrackingFinished = true;
                enableTracking = false;
                
                string summaryLine = string.Format(System.Globalization.CultureInfo.InvariantCulture, "[finish],duration={0:F1},mode={1},steps={2},foley={3},variety={4:F3},repetitions={5}",
                    trackingDuration,
                    activeModeLabel,
                    totalSteps,
                    totalFoleyEvents,
                    GetVarietyIndex(),
                    consecutiveRepetitionsCount
                );
                WriteLogLine(summaryLine);

#if UNITY_EDITOR
                if (autoQuitOnFinish)
                {
                    UnityEditor.EditorApplication.isPlaying = false;
                }
#endif
                return;
            }
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

        string logLine = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2},{1},{2:F1},{3:F2},{4:F2},{5},{6},{7},{8},{9}",
            Time.time,
            activeModeLabel,
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

    public void LogStepEvent(string characterName, string footName, bool isLeft, float speed, string surfaceTag, string clipName, float volume, float pitch, bool isFoley)
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
            isConsecutiveRepetition = isRepetition,
            characterName = characterName,
            footName = footName
        };

        stepHistory.Add(record);
        if (stepHistory.Count > MaxHistoryCount)
        {
            stepHistory.RemoveAt(0);
        }

        string side = isFoley ? "foley" : (isLeft ? "left" : "right");
        string logLine = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2},[step],char={1},foot={2},side={3},speed={4:F2},surface={5},clip={6},volume={7:F3},pitch={8:F3},rep={9}",
            Time.time,
            characterName,
            footName,
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
    public string GetLogPath() => logPath;

    public float GetVarietyIndex()
    {
        if (recentClips.Count == 0) return 1f;
        HashSet<string> unique = new HashSet<string>(recentClips);
        return (float)unique.Count / recentClips.Count;
    }
}
