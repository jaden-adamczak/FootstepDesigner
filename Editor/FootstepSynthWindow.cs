#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class FootstepSynthWindow : EditorWindow
{
    private enum SoundType { Wood, Grass, Stone }
    private SoundType selectedSound = SoundType.Wood;

    private enum ExportFormat { WAV, RAW }
    private ExportFormat exportFormat = ExportFormat.WAV;

    private const int SampleRate = 44100;

    public class SynthParameter
    {
        public string name;
        public float value;
        public float min;
        public float max;

        public SynthParameter(string name, float value, float min, float max)
        {
            this.name = name;
            this.value = value;
            this.min = min;
            this.max = max;
        }
    }

    private Dictionary<SoundType, List<SynthParameter>> synthParams = new Dictionary<SoundType, List<SynthParameter>>();

    private string exportFolder = "Assets/LocalSynth";
    private string exportFileName = "Footstep_Synthetic";

    private float[] previewSamples;
    private float[] amplitudeEnvelope;
    private AudioClip previewClip;

    [MenuItem("Tools/Footsteps/Footstep Synth Generator")]
    public static void ShowWindow()
    {
        GetWindow<FootstepSynthWindow>("Footstep Synth Generator");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Footstep Sound Synthesizer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("synthesize procedural sound textures for footsteps using physical models.", MessageType.Info);
        GUILayout.Space(5);

        selectedSound = (SoundType)EditorGUILayout.EnumPopup("Sound Type", selectedSound);
        GUILayout.Space(10);

        DrawParameters();

        GUILayout.Space(15);
        EditorGUILayout.LabelField("Export Settings", EditorStyles.boldLabel);
        exportFolder = EditorGUILayout.TextField("Export Folder", exportFolder);
        exportFileName = EditorGUILayout.TextField("File Name", exportFileName);
        exportFormat = (ExportFormat)EditorGUILayout.EnumPopup("Format", exportFormat);

        GUILayout.Space(15);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Synthesize & Preview", GUILayout.Height(30)))
        {
            GeneratePreview();
        }
        if (GUILayout.Button("Bake and Save File", GUILayout.Height(30)))
        {
            BakeAndSave();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(15);
        DrawEnvelope();
    }

    private void OnEnable()
    {
        InitializeParameters();
    }

    private void InitializeParameters()
    {
        synthParams[SoundType.Wood] = new List<SynthParameter>
        {
            new SynthParameter("Duration (s)", 0.3f, 0.1f, 1f),
            new SynthParameter("Thud Freq (Hz)", 120f, 40f, 300f),
            new SynthParameter("Thud Decay", 0.05f, 0.005f, 0.2f),
            new SynthParameter("Click Freq (Hz)", 700f, 300f, 1500f),
            new SynthParameter("Click Decay", 0.01f, 0.001f, 0.05f),
            new SynthParameter("Noise Level", 0.3f, 0f, 1f),
            new SynthParameter("Noise Decay", 0.008f, 0.001f, 0.05f),
            new SynthParameter("Thud vs Click Mix", 0.7f, 0f, 1f)
        };

        synthParams[SoundType.Grass] = new List<SynthParameter>
        {
            new SynthParameter("Duration (s)", 0.25f, 0.1f, 1f),
            new SynthParameter("HP Filter Cutoff (Alpha)", 0.15f, 0.01f, 0.5f),
            new SynthParameter("Attack (s)", 0.02f, 0.001f, 0.05f),
            new SynthParameter("Decay (s)", 0.07f, 0.01f, 0.3f),
            new SynthParameter("Crunch Density", 0.05f, 0f, 0.2f),
            new SynthParameter("Crunch Amplitude", 0.2f, 0f, 0.5f)
        };

        synthParams[SoundType.Stone] = new List<SynthParameter>
        {
            new SynthParameter("Duration (s)", 0.2f, 0.1f, 1f),
            new SynthParameter("Resonance Freq 1 (Hz)", 1800f, 500f, 3000f),
            new SynthParameter("Resonance Freq 2 (Hz)", 950f, 300f, 2000f),
            new SynthParameter("Resonance Decay", 0.012f, 0.002f, 0.1f),
            new SynthParameter("Noise Level", 0.4f, 0f, 1f),
            new SynthParameter("Noise Decay", 0.015f, 0.002f, 0.1f),
            new SynthParameter("Tone vs Noise Mix", 0.5f, 0f, 1f)
        };
    }

    private float GetParam(SoundType type, string name)
    {
        if (synthParams.TryGetValue(type, out var list))
        {
            var p = list.Find(x => x.name == name);
            if (p != null) return p.value;
        }
        return 0f;
    }

    private void DrawParameters()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"{selectedSound} Parameter Settings", EditorStyles.boldLabel);
        GUILayout.Space(5);

        if (synthParams.TryGetValue(selectedSound, out var list))
        {
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                p.value = EditorGUILayout.Slider(p.name, p.value, p.min, p.max);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void GeneratePreview()
    {
        previewSamples = GenerateSamplesArray();
        CalculateEnvelope();

        if (previewSamples == null || previewSamples.Length == 0) return;

        previewClip = AudioClip.Create("SynthPreview", previewSamples.Length, 1, SampleRate, false);
        previewClip.SetData(previewSamples, 0);

        PlayPreviewClip(previewClip);
    }

    private float[] GenerateSamplesArray()
    {
        switch (selectedSound)
        {
            case SoundType.Wood:
                return GenerateWood();
            case SoundType.Grass:
                return GenerateGrass();
            case SoundType.Stone:
                return GenerateStone();
            default:
                return null;
        }
    }

    private float[] GenerateWood()
    {
        float woodDuration = GetParam(SoundType.Wood, "Duration (s)");
        float woodThudFreq = GetParam(SoundType.Wood, "Thud Freq (Hz)");
        float woodThudDecay = GetParam(SoundType.Wood, "Thud Decay");
        float woodClickFreq = GetParam(SoundType.Wood, "Click Freq (Hz)");
        float woodClickDecay = GetParam(SoundType.Wood, "Click Decay");
        float woodNoiseLevel = GetParam(SoundType.Wood, "Noise Level");
        float woodNoiseDecay = GetParam(SoundType.Wood, "Noise Decay");
        float woodMix = GetParam(SoundType.Wood, "Thud vs Click Mix");

        int numSamples = (int)(SampleRate * woodDuration);
        float[] samples = new float[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / SampleRate;
            float sineThud = Mathf.Sin(2f * Mathf.PI * woodThudFreq * t);
            float envThud = Mathf.Exp(-t / woodThudDecay);

            float sineClick = Mathf.Sin(2f * Mathf.PI * woodClickFreq * t);
            float envClick = Mathf.Exp(-t / woodClickDecay);

            float noise = Random.Range(-1f, 1f);
            float envNoise = Mathf.Exp(-t / woodNoiseDecay);

            float toneComponent = (sineThud * envThud * woodMix) + (sineClick * envClick * (1f - woodMix));
            samples[i] = (toneComponent * 0.7f) + (noise * woodNoiseLevel * envNoise * 0.3f);
        }

        return Normalize(samples);
    }

    private float[] GenerateGrass()
    {
        float grassDuration = GetParam(SoundType.Grass, "Duration (s)");
        float grassFilterAlpha = GetParam(SoundType.Grass, "HP Filter Cutoff (Alpha)");
        float grassAttack = GetParam(SoundType.Grass, "Attack (s)");
        float grassDecay = GetParam(SoundType.Grass, "Decay (s)");
        float grassCrunchDensity = GetParam(SoundType.Grass, "Crunch Density");
        float grassCrunchAmp = GetParam(SoundType.Grass, "Crunch Amplitude");

        int numSamples = (int)(SampleRate * grassDuration);
        float[] samples = new float[numSamples];

        float z = 0f;
        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / SampleRate;
            float rawNoise = Random.Range(-1f, 1f);

            z = z + grassFilterAlpha * (rawNoise - z);
            float hpNoise = rawNoise - z;

            float env;
            if (t < grassAttack)
            {
                env = t / grassAttack;
            }
            else
            {
                env = Mathf.Exp(-(t - grassAttack) / grassDecay);
            }

            float sampleVal = hpNoise * env;

            if (Random.value < grassCrunchDensity)
            {
                sampleVal += Random.Range(-1f, 1f) * grassCrunchAmp * env;
            }

            samples[i] = sampleVal;
        }

        return Normalize(samples);
    }

    private float[] GenerateStone()
    {
        float stoneDuration = GetParam(SoundType.Stone, "Duration (s)");
        float stoneFreq1 = GetParam(SoundType.Stone, "Resonance Freq 1 (Hz)");
        float stoneFreq2 = GetParam(SoundType.Stone, "Resonance Freq 2 (Hz)");
        float stoneDecay = GetParam(SoundType.Stone, "Resonance Decay");
        float stoneNoiseLevel = GetParam(SoundType.Stone, "Noise Level");
        float stoneNoiseDecay = GetParam(SoundType.Stone, "Noise Decay");
        float stoneMix = GetParam(SoundType.Stone, "Tone vs Noise Mix");

        int numSamples = (int)(SampleRate * stoneDuration);
        float[] samples = new float[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / SampleRate;
            float sine1 = Mathf.Sin(2f * Mathf.PI * stoneFreq1 * t);
            float sine2 = Mathf.Sin(2f * Mathf.PI * stoneFreq2 * t);
            float ringEnv = Mathf.Exp(-t / stoneDecay);

            float noise = Random.Range(-1f, 1f);
            float noiseEnv = Mathf.Exp(-t / stoneNoiseDecay);

            float tone = (sine1 + sine2) * 0.5f * ringEnv;
            samples[i] = (tone * stoneMix) + (noise * stoneNoiseLevel * noiseEnv * (1f - stoneMix));
        }

        return Normalize(samples);
    }

    private float[] Normalize(float[] samples)
    {
        float maxVal = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float absVal = Mathf.Abs(samples[i]);
            if (absVal > maxVal) maxVal = absVal;
        }

        if (maxVal > 0f)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] /= maxVal;
            }
        }
        return samples;
    }

    private void CalculateEnvelope()
    {
        if (previewSamples == null || previewSamples.Length == 0)
        {
            amplitudeEnvelope = null;
            return;
        }

        int width = 200;
        amplitudeEnvelope = new float[width];
        int step = previewSamples.Length / width;
        if (step <= 0) step = 1;

        for (int i = 0; i < width; i++)
        {
            float max = 0f;
            int start = i * step;
            int end = Mathf.Min(start + step, previewSamples.Length);
            for (int j = start; j < end; j++)
            {
                float absVal = Mathf.Abs(previewSamples[j]);
                if (absVal > max) max = absVal;
            }
            amplitudeEnvelope[i] = max;
        }
    }

    private void DrawEnvelope()
    {
        if (amplitudeEnvelope == null || amplitudeEnvelope.Length == 0) return;

        EditorGUILayout.LabelField("Waveform Amplitude Envelope", EditorStyles.boldLabel);
        Rect rect = GUILayoutUtility.GetRect(100, 80, GUILayout.ExpandWidth(true));
        GUI.Box(rect, "");

        float halfHeight = rect.height / 2f;
        float middleY = rect.y + halfHeight;
        float stepX = rect.width / amplitudeEnvelope.Length;

        Color oldColor = GUI.color;
        GUI.color = new Color(0.1f, 0.7f, 1f, 0.8f);

        for (int i = 0; i < amplitudeEnvelope.Length; i++)
        {
            float val = amplitudeEnvelope[i];
            float x = rect.x + i * stepX;
            float h = val * halfHeight;
            GUI.DrawTexture(new Rect(x, middleY - h, Mathf.Max(1f, stepX - 1f), h * 2f), EditorGUIUtility.whiteTexture);
        }

        GUI.color = oldColor;
    }

    private void BakeAndSave()
    {
        float[] samples = GenerateSamplesArray();
        if (samples == null || samples.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "No samples generated. Click Synthesize & Preview first.", "OK");
            return;
        }

        if (!Directory.Exists(exportFolder))
        {
            Directory.CreateDirectory(exportFolder);
        }

        string extension = exportFormat == ExportFormat.WAV ? "wav" : "raw";
        string defaultName = $"{exportFileName}.{extension}";
        string absoluteFolderPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", exportFolder));

        string filePath = EditorUtility.SaveFilePanel("Save Synthetic Audio", absoluteFolderPath, defaultName, extension);

        if (string.IsNullOrEmpty(filePath)) return;

        if (exportFormat == ExportFormat.WAV)
        {
            WriteWavFile(filePath, samples, 1, SampleRate);
        }
        else
        {
            WriteRawFile(filePath, samples);
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Success", $"Baked and saved synthetic sound at:\n{filePath}", "OK");
    }

    private void WriteRawFile(string filePath, float[] samples)
    {
        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            using (var writer = new BinaryWriter(fileStream))
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    writer.Write(samples[i]);
                }
            }
        }
    }

    private void WriteWavFile(string filePath, float[] samples, int channels, int sampleRate)
    {
        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            using (var writer = new BinaryWriter(fileStream))
            {
                short bitsPerSample = 16;
                int byteRate = sampleRate * channels * bitsPerSample / 8;
                short blockAlign = (short)(channels * bitsPerSample / 8);
                int subChunk2Size = samples.Length * bitsPerSample / 8;
                int chunkSize = 36 + subChunk2Size;

                writer.Write(Encoding.UTF8.GetBytes("RIFF"));
                writer.Write(chunkSize);
                writer.Write(Encoding.UTF8.GetBytes("WAVE"));

                writer.Write(Encoding.UTF8.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write(bitsPerSample);

                writer.Write(Encoding.UTF8.GetBytes("data"));
                writer.Write(subChunk2Size);

                for (int i = 0; i < samples.Length; i++)
                {
                    float sample = Mathf.Clamp(samples[i], -1f, 1f);
                    short shortSample = (short)(sample * 32767f);
                    writer.Write(shortSample);
                }
            }
        }
    }

    private static void PlayPreviewClip(AudioClip clip)
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
            Debug.LogWarning("failed to preview clip: " + e.Message);
        }
    }
}
#endif
