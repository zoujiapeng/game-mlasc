# Lumen Circuit

Lumen Circuit is an original, family-friendly arcade kart racer built in Unity. It targets the readable handling, drift rhythm, chase-camera clarity, item tension, and catch-up pacing associated with the arcade kart-racing genre while deliberately avoiding Nintendo characters, names, track layouts, UI, sounds, music, item designs, and other protected presentation.

The first repository milestone is a code-generated vertical slice: one complete circuit, eight racers, three laps, drift mini-turbos, boost pads, original pickup items, rubber-band AI, race HUD, procedural audio, and a soft low-saturation visual direction. No external art package is required to open the prototype.

## Open and play

1. Install **Unity 6.3 LTS, editor 6000.3.20f1**, with Windows Build Support.
2. Clone this repository and add the folder in Unity Hub.
3. Open the project and allow package/script import to finish.
4. The editor bootstrap creates `Assets/LumenKart/Generated` and `Assets/LumenKart/Scenes/LumenCircuit.unity` automatically.
5. Open the generated scene if it is not already open, then press Play.

You can rebuild the generated content at any time from **Lumen Circuit > Rebuild Prototype**.

## Controls

| Action | Keyboard | Common gamepad mapping through Unity Input Manager |
|---|---|---|
| Accelerate | W / Up | Left stick up / configured vertical axis |
| Brake / reverse | S / Down | Left stick down / configured vertical axis |
| Steer | A/D / Left/Right | Left stick |
| Drift / hop | Space / Left Shift | Joystick button 0 or 5 |
| Use item | E / Right Ctrl | Joystick button 1 or 4 |
| Restart race | R | — |
| Pause | Esc | Start / joystick button 7 |

## Current scope

- Physics-driven arcade kart handling with speed-sensitive steering.
- Hop-to-drift transition and three mini-turbo charge tiers.
- Custom chase camera with look-ahead, dynamic FOV, drift offset, and collision avoidance.
- Eight-racer race loop with countdown, ordered checkpoints, lap validation, ranking, finish results, and respawn.
- AI racing line, corner anticipation, drift decisions, stuck recovery, and restrained rubber-banding.
- Original items: Burst Drive, Arc Pulse, Cloud Shield, and Glimmer Trap.
- Procedurally generated track, karts, props, materials, particles, and synthesized placeholder audio.
- URP lighting, bloom, color grading, fog, soft shadows, and a muted fresh palette.

## Repository policy

Only original work, public-domain/CC0 assets, or assets with a license that explicitly permits commercial game use may be added. Every third-party asset must be listed in `THIRD_PARTY_NOTICES.md` with its source and license. “Found online” is not a license.

See `docs/` for design targets, art direction, development workflow, and IP constraints.
