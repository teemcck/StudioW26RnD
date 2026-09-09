# Studio W26 R&D

Open this folder as a project in **Unity 6000.3.6f1**. Start with `Assets/Scenes/Game/MenuScene.unity` for the game flow or `Assets/Scenes/Game/BossGameplay.unity` for the boss encounter.

## Project map

| Folder | What belongs here |
| --- | --- |
| `Assets/Art` | Animation clips/controllers, fonts, materials, shaders, sprites, placeholders, and tiles |
| `Assets/Audio` | Music by world/encounter, sound effects, and audio mixers |
| `Assets/Data` | Authored ScriptableObject assets, including upgrade displays and effects |
| `Assets/Prefabs` | Reusable enemies, player, projectiles, map chunks, UI, rendering, and visual effects |
| `Assets/Scenes/Game` | The five enabled game scenes, in their existing build order |
| `Assets/Scenes/Sandbox` | Individual art and upgrade experiments |
| `Assets/Scenes/Recovery` | The preserved recovery scene |
| `Assets/Scripts` | C# source grouped by feature; see the guide below |
| `Assets/Settings` | Input, render pipeline, volume, and UI Toolkit configuration |
| `Assets/Resources` | Assets deliberately loaded through Unity's `Resources` API |
| `Assets/TextMesh Pro` | Imported TMP resources, retained in their existing location |
| `Docs` | Code guides and project context |
| `Packages`, `ProjectSettings` | Unity dependencies and project configuration |

Tile palettes stay with their tiles under `Assets/Art/Tiles/Main` and `Legacy`. The legacy tiles, sandbox scenes, and recovery scene remain available; moving them does not imply they are unused.

## Script map

| Folder under `Assets/Scripts` | Responsibility |
| --- | --- |
| `Core` | Run coordination, statistics, shared constants, and typed events |
| `Audio`, `Camera` | Shared music/audio and camera control |
| `Combat` | Damage contracts, enemies, weapons, projectiles, and hit feedback |
| `Combat/Boss` | Worm boss controller modules and navigation, with `Cinematics` and `Presentation` support folders |
| `Player` | Player state, movement, combat state, status effects, upgrades, and visual feedback |
| `Progression` | Floor scaling rules and their settings type |
| `Rendering` | Lighting, volume control, and world ambience |
| `UI` | Screens, HUD, cards, tooltips, upgrade displays, transitions, and combat numbers |
| `Upgrades` | Upgrade selection, definitions, effects, and startup debugging |
| `World` | Map generation, enemy spawning, and teleportation |

General UI belongs in `Scripts/UI`; encounter-specific presentation stays with the boss. ScriptableObject **types** belong in `Scripts`, and their authored **instances** belong in `Data`, unless a specific loading API requires a different location. `FloorScalingCurve` uses `Resources.Load("FloorScaling")`; a settings override must keep that resource key.
