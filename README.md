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

## Keeping the project organized

- Move or rename assets through Unity's Project window so their `.meta` files travel with them. Outside Unity, move both together while the Editor is closed.
- Keep component class names, script filenames, and asset GUIDs stable when only reorganizing folders. The code currently shares Unity's default `Assembly-CSharp` assembly.
- Keep boss partial files beside `WormBossController.cs`; attach that main component to GameObjects. See the [boss code guide](Docs/BossBattle.md) for module responsibilities and scene wiring.
- When moving game scenes, update their paths in Build Profiles while preserving scene names and order. Runtime transitions load those scene names.
- Keep imported package resources and special folders such as `Resources` in locations compatible with their loaders.

The VS Code Explorer shows authored assets and project settings, hides Unity caches and metadata, and nests the boss partial files under the main controller. These display rules are in `.vscode/settings.json`. Existing Rider shelves, personal IDE files, and the `Temp 2` backup are preserved on disk and hidden from the Explorer; the ignore rules prevent new copies of that clutter from being added.

See [Unity project context](Docs/AI/UnityProjectContext.md) for dependencies, scene flow, and validation history.
