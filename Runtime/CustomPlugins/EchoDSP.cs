using System.Collections.Generic;
using UnityEngine;

public class EchoDSP : IFootstepDSP
{
    public string Name => "Echo Modifier";
    public bool Enabled { get; set; } = false;
    public List<DSPParameter> Parameters { get; } = new List<DSPParameter>
    {
        new DSPParameter("Delay (ms)", 100f, 10f, 500f, "time delay between echoes in milliseconds"),
        new DSPParameter("Feedback", 0.4f, 0f, 0.95f, "strength of echo feedback loop"),
        new DSPParameter("Mix", 0f, 0f, 1f, "wet dry audio mix ratio"),
        new DSPParameter("Low Pass Dampening", 0.8f, 0.05f, 1f, "cutoff coefficient for high frequencies"),
        new DSPParameter("High Pass Dampening", 0.2f, 0f, 0.95f, "cutoff coefficient for low frequencies")
    };

    public float[] Apply(float[] samples, int channels, int frequency)
    {
        float delayTimeMs = Parameters[0].value;
        float feedback = Parameters[1].value;
        float mix = Parameters[2].value;
        float lowPassCutoff = Parameters[3].value;
        float highPassCutoff = Parameters[4].value;

        if (mix <= 0f || delayTimeMs <= 0f) return samples;

        int delaySamples = Mathf.RoundToInt((delayTimeMs / 1000f) * frequency);
        if (delaySamples <= 0) return samples;

        float[][] delayBuffers = new float[channels][];
        for (int c = 0; c < channels; c++)
        {
            delayBuffers[c] = new float[delaySamples];
        }
        int delayIndex = 0;

        float[] prevOutput = new float[channels];
        float[] prevInput = new float[channels];

        for (int i = 0; i < samples.Length; i += channels)
        {
            for (int c = 0; c < channels; c++)
            {
                if (i + c >= samples.Length) continue;

                float dry = samples[i + c];
                float delayed = delayBuffers[c][delayIndex];

                float filtered = delayed;

                if (lowPassCutoff < 1.0f)
                {
                    filtered = prevOutput[c] + lowPassCutoff * (filtered - prevOutput[c]);
                    prevOutput[c] = filtered;
                }

                if (highPassCutoff > 0.0f)
                {
                    float nextHigh = highPassCutoff * (prevInput[c] + filtered - prevInput[c]);
                    prevInput[c] = filtered;
                    filtered = nextHigh;
                }

                delayBuffers[c][delayIndex] = dry + filtered * feedback;
                samples[i + c] = (1f - mix) * dry + mix * filtered;
            }
            delayIndex = (delayIndex + 1) % delaySamples;
        }
        return samples;
    }
}
