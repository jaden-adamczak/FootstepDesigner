using System.Collections.Generic;
using UnityEngine;

public class BitcrushDSP : IFootstepDSP
{
    public string Name => "Bitcrusher";
    public bool Enabled { get; set; } = true;
    public List<DSPParameter> Parameters { get; } = new List<DSPParameter>
    {
        new DSPParameter("Bit Depth", 16f, 1f, 16f, "target resolution of audio samples")
    };

    public float[] Apply(float[] samples, int channels, int frequency)
    {
        float bitDepth = Parameters[0].value;
        if (bitDepth >= 16f) return samples;

        float steps = Mathf.Pow(2f, bitDepth - 1f);
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = Mathf.Round(samples[i] * steps) / steps;
        }
        return samples;
    }
}
