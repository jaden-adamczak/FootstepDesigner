using System.Collections.Generic;
using UnityEngine;

public class FeedbackDelayDSP : IFootstepDSP
{
    public string Name => "Feedback Delay";
    public bool Enabled { get; set; } = true;
    public List<DSPParameter> Parameters { get; } = new List<DSPParameter>
    {
        new DSPParameter("Delay (ms)", 50f, 10f, 200f, "time delay between echoes in milliseconds"),
        new DSPParameter("Feedback", 0.3f, 0f, 0.9f, "strength of echo feedback loop"),
        new DSPParameter("Mix", 0.3f, 0f, 1f, "wet dry audio mix ratio")
    };

    public float[] Apply(float[] samples, int channels, int frequency)
    {
        float delayTimeMs = Parameters[0].value;
        float decayRate = Parameters[1].value;
        float mix = Parameters[2].value;

        if (mix <= 0f || delayTimeMs <= 0f) return samples;

        int delaySamples = Mathf.RoundToInt((delayTimeMs / 1000f) * frequency);
        if (delaySamples <= 0) return samples;

        float[][] delayBuffers = new float[channels][];
        for (int c = 0; c < channels; c++)
        {
            delayBuffers[c] = new float[delaySamples];
        }
        int delayIndex = 0;

        for (int i = 0; i < samples.Length; i += channels)
        {
            for (int c = 0; c < channels; c++)
            {
                if (i + c >= samples.Length) continue;

                float dry = samples[i + c];
                float delayed = delayBuffers[c][delayIndex];

                delayBuffers[c][delayIndex] = dry + delayed * decayRate;
                samples[i + c] = (1f - mix) * dry + mix * delayed;
            }
            delayIndex = (delayIndex + 1) % delaySamples;
        }
        return samples;
    }
}
