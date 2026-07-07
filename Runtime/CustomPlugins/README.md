# Custom DSP Plugins

This directory contains digital signal processing (DSP) plugins for FootstepDesigner.

## How to Create Your Own DSP Plugin

To add a new DSP plugin:

1. Create a new C# file in this directory.
2. Implement the `IFootstepDSP` interface defined in `IFootstepDSP.cs`.
3. Add your plugin to a assembly definition if required.
4. Define parameters using the `DSPParameter` helper class.
5. Implement the `Apply` method to modify the float audio samples array.
6. The editor window will automatically discover your class and draw its sliders in the user interface.

## Useful Audio Terminology

- **Sampling Rate (Frequency):** The number of audio samples processed per second (typically 44100 Hz).
- **Interleaved Channels:** For multi-channel audio, samples are stored sequentially (e.g. left sample, right sample, left sample, right sample).
- **Single-Pole Low-Pass Filter:** Used to dampen high frequencies. The formula is:
  `y[n] = y[n-1] + alpha * (x[n] - y[n-1])`
- **Bit Depth:** The resolution of each amplitude sample. Lowering it introduces crushing noise.
