using System.Collections.Generic;

// template showing interface implementation for developers
// this file is not compiled into runtime audio execution
public class TemplateDSP : IFootstepDSP
{
    // name of the dsp effect
    public string Name => "Template Effect";

    // toggle state of the effect
    public bool Enabled { get; set; } = false;

    // parameter array representing slider values
    // float values represent gains, frequencies, or time values
    public List<DSPParameter> Parameters { get; } = new List<DSPParameter>
    {
        new DSPParameter("param name", 1f, 0f, 2f, "parameter tooltip text")
    };

    // apply digital signal processing logic to float samples
    // samples is an interleaved float array representing audio data
    // channels is the number of audio channels (e.g. 1 for mono, 2 for stereo)
    // frequency is the sampling rate in hz (e.g. 44100hz)
    public float[] Apply(float[] samples, int channels, int frequency)
    {
        // loop over samples and apply math
        // for stereo audio, left and right channels alternate: samples[i] and samples[i+1]
        // delay buffers can track historical sample history to create reverb or delay
        // single pole filter formula: y[n] = x[n] * alpha + y[n-1] * (1 - alpha)
        
        // return modified sample array
        return samples;
    }
}
