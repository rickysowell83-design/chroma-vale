# Chroma Vale — Audio Assets

This directory holds the AudioClip assets (`.wav` / `.ogg` files) for Chroma Vale's sound effects.

## Required Sound Effects

The game references 8 named sounds via `AudioLibrary.asset`. Create or source the following files and assign them in the Unity Editor at `Assets/Audio/AudioLibrary.asset`:

| Sound Name       | Description                          | Suggested Source / Style                        |
|------------------|--------------------------------------|-------------------------------------------------|
| `pipe_place`     | Player places a pipe piece on grid   | Short snap/click, ~0.1s                         |
| `flow_tick`      | Flow advances into a pipe cell       | Quick whoosh/pop, ~0.15s                        |
| `pipe_burst`     | Pipe bursts under overload           | Break/crack sound, ~0.3s                        |
| `target_reached` | Flow successfully reaches a target   | Bright chime/ding, ~0.3s                        |
| `win_fanfare`    | Level complete celebration            | Short fanfare/jingle, ~1.5s                     |
| `color_mix`      | Two flow colors mix                   | Swirl/bubble blend sound, ~0.3s                 |
| `undo`           | Player undoes a piece placement       | Quick reverse-swoosh pop, ~0.15s                |
| `level_start`    | Level begins / loads                  | Subtle whoosh or startup tone, ~0.5s            |

## Sourcing

- **freesound.org** — Search for CC0 / Creative Commons 0 licensed sound effects.
  - Keywords: "snap", "click", "pop", "whoosh", "chime", "break", "fanfare", "bubble", "swirl"
  - Filter by: License → Creative Commons 0, Duration → short (< 1s for most)
- **kenney.nl** — Public domain sound effect packs (e.g., "Kenney Impact Sounds", "Kenney UI Audio")
- **zapsplat.com** — Free tier with attribution
- **Mixkit.co** — Free sound effects, no attribution required
- **Self-recorded** — Record simple sounds with a phone or microphone, edit in Audacity

## File Format

- Format: `44100 Hz, 16-bit, mono, .wav`
- Naming: `{sound_name}.wav` (match the sound name exactly)
- Normalize to approximately -3 dB peak amplitude for consistent volume
- Trim silence from start/end of each file

## How to Assign

1. Place `.wav` files in this folder (`Assets/Audio/`)
2. Open `Assets/Audio/AudioLibrary.asset` in the Unity Inspector
3. Expand the `Sounds` array
4. For each entry, drag the matching AudioClip from the Project window onto the `Clip` field
5. Press **Ctrl+R** to refresh the Unity Editor

## Testing Without Audio Files

The `AudioService` falls back to `Debug.Log($"[Audio] {soundName}")` when no clip is assigned for a sound. This means the game is fully functional without audio assets — you will see `[Audio] pipe_place` etc. in the Unity Console instead of hearing sounds.

## Attribution (if required by source)

If you source sounds that require attribution, list them here with the author name and license URL.
