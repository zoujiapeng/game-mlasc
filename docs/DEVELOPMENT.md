# Development workflow

## Required editor

Unity 6.3 LTS, `6000.3.20f1`, using URP 17.3.

## Generated content

`PrototypeBuilder` creates the initial scene and generated assets. Source code lives under `Assets/LumenKart/Runtime` and `Assets/LumenKart/Editor`; generated materials, profiles, meshes, and the scene live under `Assets/LumenKart/Generated` and `Assets/LumenKart/Scenes` after first open.

Use **Lumen Circuit > Rebuild Prototype** to regenerate the vertical slice. This command intentionally replaces generated content. Do not place hand-authored production assets inside the generated directory.

## Validation

Run:

```bash
python Tools/validate_project.py
```

This checks JSON manifests, project version, prohibited borrowed-brand terms in source paths/content, and basic C# delimiter balance. It is not a Unity compiler. The definitive check is opening the project in the pinned editor and entering Play Mode.

## Branching

- `main`: reviewed, playable state.
- `agent/*`: implementation branches.
- Keep generated binary assets out of commits unless they are required for deterministic production content.
- Use Git LFS for textures, audio, models, and other large binary assets.

## Next engineering milestones

1. Real Unity compile and play-mode validation on Windows.
2. Handling telemetry and automated lap-time test driver.
3. Split-screen architecture and input-device pairing.
4. Production kart rig and animation pipeline.
5. Audio replacement with original authored assets.
6. Steam Input, Steamworks, settings, save data, achievements, and build automation.
