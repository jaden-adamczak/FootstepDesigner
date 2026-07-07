using System.Collections.Generic;
using UnityEngine;

public class DownsampleDSP : IFootstepDSP
{
    public string Name => "Downsampler";
    public bool Enabled { get; set; } = false;
    public List<DSPParameter> Parameters { get; } = new List<DSPParameter>
    {
        new DSPParameter("Factor", 1f, 1f, 16f, "amount of sample rate reduction")
    };

    public float[] Apply(float[] samples, int channels, int frequency)
    {
        int factor = Mathf.RoundToInt(Parameters[0].value);
        if (factor <= 1) return samples;

        for (int i = 0; i < samples.Length; i += channels)
        {
            int baseIndex = (i / (factor * channels)) * (factor * channels);
            for (int c = 0; c < channels; c++)
            {
                if (baseIndex + c < samples.Length && i + c < samples.Length)
                {
                    samples[i + c] = samples[baseIndex + c];
                }
            }
        }
        return samples;
    }
}
