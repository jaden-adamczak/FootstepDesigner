#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class FootstepSynthWindow : EditorWindow
{
    private enum SoundType { Wood, Grass, Sand, Stone }
    private SoundType selectedSound = SoundType.Wood;

    private enum ExportFormat { WAV, RAW, MP3, OGG }
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
    private float masterGain = 1.0f;

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
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        masterGain = EditorGUILayout.Slider("Master Gain", masterGain, 0.1f, 3.0f);
        GUILayout.Label(new UnityEngine.GUIContent(EditorGUIUtility.IconContent("console.infoIcon").image, "Multiply synthesized amplitude before preview or baking. Use > 1.0 to boost quiet sounds like grass."), GUILayout.Width(16), GUILayout.Height(16));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Synthesize & Preview", GUILayout.Height(30)))
        {
            GeneratePreview();
        }
        if (previewClip != null)
        {
            if (GUILayout.Button("Play Sound", GUILayout.Height(30)))
            {
                PlayPreviewClip(previewClip);
            }
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

    /// <summary>
    /// Initialises default synthesis parameters for each sound type.
    /// 
    /// The Wood and Stone models use modal synthesis — a sum of decaying sinusoids driven
    /// by an impact force — following the framework described in:
    ///   van den Doel, K., Kry, P. G., &amp; Pai, D. K. (2001).
    ///   FoleyAutomatic: Physically-based sound effects for interactive simulation and animation.
    ///   Proceedings of SIGGRAPH 2001, 537-544.
    /// Parameter values (resonant frequencies, decay times) were tuned empirically against
    /// reference recordings to match perceptually plausible material characteristics.
    /// 
    /// The Grass and Sand models use HP-filtered white noise with an attack-decay envelope,
    /// approximating the stochastic texture model described in the granular synthesis
    /// literature (see DAFx conference proceedings on auditory texture synthesis).
    /// </summary>
    private void InitializeParameters()
    {
        synthParams[SoundType.Wood] = new List<SynthParameter>
        {
            new SynthParameter("Duration (s)",       0.185f, 0.1f,  1f),
            new SynthParameter("Thud Freq (Hz)",     78.9f,  20f,   300f),
            new SynthParameter("Thud Decay",         0.0457f,0.005f,0.2f),
            new SynthParameter("Click Freq (Hz)",    357f,   100f,  1500f),
            new SynthParameter("Click Decay",        0.0049f,0.001f,0.05f),
            new SynthParameter("Noise Level",        0.067f, 0f,    1f),
            new SynthParameter("Noise Decay",        0.008f, 0.001f,0.05f),
            new SynthParameter("Thud vs Click Mix",  0.55f,  0f,    1f)
        };

        synthParams[SoundType.Grass] = new List<SynthParameter>
        {
            new SynthParameter("Duration (s)",            0.18f,   0.1f,  1f),
            new SynthParameter("HP Filter Cutoff (Alpha)",0.04f,   0.005f,0.5f),
            new SynthParameter("Attack (s)",              0.018f,  0.001f,0.1f),
            new SynthParameter("Decay (s)",               0.0834f, 0.01f, 0.4f),
            new SynthParameter("Crunch Density",          0.1161f, 0f,    0.3f),
            new SynthParameter("Crunch Amplitude",        0.221f,  0f,    0.8f),
            new SynthParameter("Stem Buzz Level",         0.081f,  0f,    1f),
            new SynthParameter("Stem Buzz Decay",         0.0354f, 0.005f,0.2f)
        };

        synthParams[SoundType.Sand] = new List<SynthParameter>
        {
            new SynthParameter("Duration (s)",            0.244f, 0.1f,  1f),
            new SynthParameter("HP Filter Cutoff (Alpha)",0.372f, 0.01f, 0.5f),
            new SynthParameter("Attack (s)",              0.0076f,0.001f,0.05f),
            new SynthParameter("Decay (s)",               0.0422f,0.01f, 0.3f),
            new SynthParameter("Crunch Density",          0.034f, 0f,    0.2f),
            new SynthParameter("Crunch Amplitude",        0.135f, 0f,    0.5f)
        };

        synthParams[SoundType.Stone] = new List<SynthParameter>
        {
            new SynthParameter("Duration (s)",          0.133f,  0.1f,  1f),
            new SynthParameter("Resonance Freq 1 (Hz)", 500f,    80f,   3000f),
            new SynthParameter("Resonance Freq 2 (Hz)", 357f,    60f,   2000f),
            new SynthParameter("Resonance Decay",       0.0094f, 0.002f,0.1f),
            new SynthParameter("Noise Level",           0.101f,  0f,    1f),
            new SynthParameter("Noise Decay",           0.015f,  0.002f,0.1f),
            new SynthParameter("Tone vs Noise Mix",     0.907f,  0f,    1f)
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

        // Apply master gain after normalization
        if (Mathf.Abs(masterGain - 1.0f) > 0.001f)
        {
            for (int i = 0; i < previewSamples.Length; i++)
            {
                previewSamples[i] = Mathf.Clamp(previewSamples[i] * masterGain, -1f, 1f);
            }
        }

        previewClip = AudioClip.Create("SynthPreview", previewSamples.Length, 1, SampleRate, false);
        previewClip.SetData(previewSamples, 0);

        PlayPreviewClip(previewClip);
    }

    private float[] GenerateSamplesArray()
    {
        switch (selectedSound)
        {
            case SoundType.Wood:  return GenerateWood();
            case SoundType.Grass: return GenerateGrass();
            case SoundType.Sand:  return GenerateSand();
            case SoundType.Stone: return GenerateStone();
            default: return null;
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

    // Grass — low, swishy rustle with prominent stem crunch and a secondary low-frequency buzz
    // The low HP alpha (0.04) passes more bass energy, giving a "body" absent in sand.
    // Stem Buzz adds a short bandpass-like tone burst to simulate stiff grass blades vibrating.
    private float[] GenerateGrass()
    {
        float duration    = GetParam(SoundType.Grass, "Duration (s)");
        float alpha       = GetParam(SoundType.Grass, "HP Filter Cutoff (Alpha)");
        float attack      = GetParam(SoundType.Grass, "Attack (s)");
        float decay       = GetParam(SoundType.Grass, "Decay (s)");
        float crunchDens  = GetParam(SoundType.Grass, "Crunch Density");
        float crunchAmp   = GetParam(SoundType.Grass, "Crunch Amplitude");
        float buzzLevel   = GetParam(SoundType.Grass, "Stem Buzz Level");
        float buzzDecay   = GetParam(SoundType.Grass, "Stem Buzz Decay");

        int numSamples = (int)(SampleRate * duration);
        float[] samples = new float[numSamples];

        // Low-pass filtered noise (inverted HP → LP dominant gives body)
        float z = 0f;
        // A very low-freq sine "buzz" to simulate stiff grass blade vibration
        float buzzFreq = 280f;

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / SampleRate;
            float rawNoise = Random.Range(-1f, 1f);

            // HP filter
            z = z + alpha * (rawNoise - z);
            float hpNoise = rawNoise - z;

            // Amplitude envelope
            float env = t < attack
                ? t / attack
                : Mathf.Exp(-(t - attack) / decay);

            float sampleVal = hpNoise * env;

            // Stem crunch transients
            if (Random.value < crunchDens)
                sampleVal += Random.Range(-1f, 1f) * crunchAmp * env;

            // Stem buzz — low sine burst decaying quickly, adds organic "blade" character
            float buzzEnv = Mathf.Exp(-t / buzzDecay);
            sampleVal += Mathf.Sin(2f * Mathf.PI * buzzFreq * t) * buzzLevel * buzzEnv * 0.4f;

            samples[i] = sampleVal;
        }

        return Normalize(samples);
    }

    // Sand — sibilant high-frequency hiss, user-tuned parameters
    private float[] GenerateSand()
    {
        float duration   = GetParam(SoundType.Sand, "Duration (s)");
        float alpha      = GetParam(SoundType.Sand, "HP Filter Cutoff (Alpha)");
        float attack     = GetParam(SoundType.Sand, "Attack (s)");
        float decay      = GetParam(SoundType.Sand, "Decay (s)");
        float crunchDens = GetParam(SoundType.Sand, "Crunch Density");
        float crunchAmp  = GetParam(SoundType.Sand, "Crunch Amplitude");

        int numSamples = (int)(SampleRate * duration);
        float[] samples = new float[numSamples];

        float z = 0f;
        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / SampleRate;
            float rawNoise = Random.Range(-1f, 1f);

            z = z + alpha * (rawNoise - z);
            float hpNoise = rawNoise - z;

            float env = t < attack
                ? t / attack
                : Mathf.Exp(-(t - attack) / decay);

            float sampleVal = hpNoise * env;

            if (Random.value < crunchDens)
                sampleVal += Random.Range(-1f, 1f) * crunchAmp * env;

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

        // Apply master gain
        if (Mathf.Abs(masterGain - 1.0f) > 0.001f)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = Mathf.Clamp(samples[i] * masterGain, -1f, 1f);
            }
        }

        if (!Directory.Exists(exportFolder))
        {
            Directory.CreateDirectory(exportFolder);
        }

        string extension = "wav";
        if (exportFormat == ExportFormat.RAW) extension = "raw";
        else if (exportFormat == ExportFormat.MP3) extension = "mp3";
        else if (exportFormat == ExportFormat.OGG) extension = "ogg";

        string defaultName = $"{exportFileName}.{extension}";
        string absoluteFolderPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", exportFolder));

        string filePath = EditorUtility.SaveFilePanel("Save Synthetic Audio", absoluteFolderPath, defaultName, extension);

        if (string.IsNullOrEmpty(filePath)) return;

        if (exportFormat == ExportFormat.WAV)
        {
            WriteWavFile(filePath, samples, 1, SampleRate);
        }
        else if (exportFormat == ExportFormat.RAW)
        {
            WriteRawFile(filePath, samples);
        }
        else
        {
            string tempWav = Path.ChangeExtension(filePath, ".wav_temp");
            WriteWavFile(tempWav, samples, 1, SampleRate);
            if (!FootstepDesignerWindow.ConvertFormat(tempWav, filePath))
            {
                string fallbackPath = Path.ChangeExtension(filePath, ".wav");
                File.Move(tempWav, fallbackPath);
                filePath = fallbackPath;
                Debug.LogWarning($"ffmpeg conversion to {extension} failed or not installed. saved as wav.");
            }
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
        if (clip == null)
        {
            Debug.LogWarning("[FootstepSynth] PlayPreviewClip called with null clip.");
            return;
        }
        EditorAudioPlayer.Play(clip);
    }

    private void OnDisable()
    {
        EditorAudioPlayer.Stop();
    }
}
#endif
