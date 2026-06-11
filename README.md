# FootstepDesigner

A modular footstep audio design tool for Unity, built on FMOD. Intended for VR developers targeting Quest 3 and similar platforms.

Dissertation project by Jaden Adamczak, TCD PG AR/VR, 2026.

## What it does

Gives VR developers a node-based editor to map, configure, and tweak footstep sounds per material. Handles per-foot surface detection, material transitions, pitch/volume control, and ships with a free default sound library.

## Stack

- Unity (2022 LTS or later)
- FMOD for Unity
- C# (editor + runtime)
- Target: Meta Quest 3 (OpenXR)

## Folder structure

```
FootstepDesigner/
  Editor/               # Editor-only code (node graph, inspectors, import tools)
  Runtime/              # Runtime code (footstep controller, detectors, FMOD bridge)
    Detection/          # Foot detection strategies (raycast, bone-based)
    Audio/              # FMOD wrapper, sound bank playback
    Data/               # ScriptableObject definitions
  Resources/            # Default config assets
  Samples~/             # Default sound library (H4 recordings)
  Documentation~/       # Architecture docs, class diagram
```

## License

TBD
