# OwO — Bonelab Utility Mod

> v4.0.0 — A comprehensive MelonLoader utility mod for BONELAB.

---

## Features

### Player

- **Flight** — Hold both grips + triggers to fly. Speed scales with hand distance from body. Supports acceleration, momentum, lock-on targeting, and custom effects.
- **Dash** — Directional boost with instantaneous or continuous modes. Hand-oriented, lock-on targeting, landing velocity kill.
- **Teleport** — Save/load positions, teleport to players, waypoint system (up to 10 saved slots).
- **Gravity Boots** — Walk on walls and ceilings. Surface detection via raycasts with tunable gravity strength and rotation speed.
- **Ragdoll** — Full-body physics: grab-based (head/neck/arms/body) and physics-based (fall/impact/launch/slip/wall-push). ARM_CONTROL mode and death persistence.
- **Force Grab** — Pull items from any distance. Supports instant/global modes, multi-item, player push/pull via Fusion.
- **God Mode** — Health stays above 0. You still take damage but cannot die.
- **Auto Run** — Hold direction to auto-run.
- **XYZ Scale** — Independently scale player X/Y/Z dimensions.
- **Freeze Player** — Freeze other players in place.

### Combat

- **Object Launcher** — Fire any spawnable from your hand. Full-auto mode, trajectory preview, multi-projectile grid, homing system with targeting filters, preset management, safety system (grip prevents firing).
- **Explosive Punch** — 6 explosion types (Normal, Super, BlackFlash, Tiny, Boom, Custom). Separate left/right hand modes with SmashBone overlay and cosmetic effects.
- **Spawn on Player** — Drop items on/above/below other players with launch force, homing, and count controls.
- **Crazy Guns** — Glow, insane damage, no recoil, insane fire rate, no weight, bounce, no reload.
- **Full Auto** — Convert any gun to full-auto (60–2000 RPM).
- **Random Explode** — Spontaneous explosions at configurable intervals/chance. B+Y controller shortcut.
- **Ground Slam** — Trigger explosions on high-velocity ground impact.
- **Explosive Impact** — Explosions on melee/thrown object hits.

### Multiplayer

- **Server Queue** — Detects "server full" disconnects and auto-queues. Polls at configurable intervals, rejoins automatically when a slot opens. Works with both code-based and lobby ID-based joins.
- **Auto Host** — Automatically hosts a friends-only lobby on Fusion login with exponential backoff retry.
- **Screen Share** — Stream desktop or window to other VR players over the network. Configurable scale, FPS (15/30/60), FFmpeg compression, public/LAN IP modes.
- **Avatar Copier** — Copy another player's avatar, cosmetics, nickname, and description. Revert support.
- **Player Info** — View player Steam IDs, avatars, and status.
- **Stare at Player** — Make your rig look at another player.

### Cosmetics

- **BodyLog Color** — Full RGBA customization of Body Log hologram, ball, line, and radial menu colors.
- **Holster Hider** — Hide holstered weapons.
- **Cosmetic Presets** — Save/load cosmetic configurations.
- **BodyLog Presets** — Save/load color schemes.
- **Weeping Angel** — Freeze when observed, move when not.

### Utilities

- **Spawn Menu** — Browse and spawn items with search and pagination.
- **Despawn All** — Clear items with filters (guns, melees, NPCs, network props, etc.) and auto-despawn timer.
- **Change Map** — Load levels by barcode with level search.
- **Force Spawner** — Spawn items at proximity.
- **Keybinds** — Rebindable keyboard shortcuts for God Mode, Dash/Flight targeting, Despawn, and menu toggles.
- **Overlay Menu** — Lightweight in-game GUI overlay separate from BoneMenu.

### Blocking & Filtering

- **Player Block** — Despawn items from specific players.
- **Item Block (Server)** — Auto-despawn specific items server-wide.
- **Local Block (Client)** — Hide items locally without network despawn.
- **Anti-Grab** — Prevent other players from grabbing you.
- **Anti-Gravity Change** — Block gravity manipulation.
- **Anti-Knockout** — Prevent knockout from damage.
- **Unbreakable Grip** — Items cannot be dropped once grabbed.

---

## Requirements

- [MelonLoader](https://melonwiki.xyz/) (0.6.x)
- [BoneLib](https://bonelib.com/)
- [LabFusion](https://github.com/Lakatrazz/BONELAB-Fusion) (for multiplayer features)

---

## Installation

1. Copy `BonelabUtilityMod.dll` to your BONELAB `Mods` folder.
2. Copy `BonelabUtilityUpdater.dll` to your BONELAB `Plugins` folder (for auto-updates).
3. Launch BONELAB — MelonLoader loads the mod automatically.

Settings are saved to `UserData/DooberUtils/config.cfg` and persist across sessions.

---

## Auto-Updater

The updater plugin (`BonelabUtilityUpdater.dll`) runs before the mod loads and checks GitHub Releases for new versions. If a newer release is found, it downloads the updated DLL and replaces the old one automatically. An "Offline Mode" preference can be set to disable update checks.

---

## Standalone Mods

Individual features are also available as separate, lightweight mods:

| Mod                          | Description                     |
| ---------------------------- | ------------------------------- |
| **StandaloneServerQueue**    | Server queue auto-rejoin system |
| **StandaloneRagdoll**        | Ragdoll physics system          |
| **StandaloneObjectLauncher** | Object launcher                 |
| **StandaloneBodyLogColor**   | Body Log color customization    |

Each standalone mod has its own BoneMenu page and MelonPreferences.

---

## Project Structure

```
BonelabUtilityMod/
├── BonelabUtilityMod.csproj        # Main project
├── AssemblyInfo.cs                  # Version & assembly metadata
├── BonelabUtility.cs               # Entry point, BoneMenu setup, whitelist
├── SettingsManager.cs               # Config persistence (INI format)
├── KeybindManager.cs                # Rebindable keyboard shortcuts
├── OverlayMenu.cs                   # In-game GUI overlay
│
├── Movement
│   ├── FlightController.cs
│   ├── DashController.cs
│   ├── TeleportController.cs
│   ├── GravityBootsController.cs
│   ├── ForceGrabController.cs
│   ├── AutoRunController.cs
│   └── PlayerTargeting.cs
│
├── Combat
│   ├── ObjectLauncher.cs
│   ├── ExplosivePunchController.cs
│   ├── PlayerSpawnController.cs
│   ├── FullAutoController.cs
│   ├── FullAutoGunSystem.cs
│   ├── ChaosGunController.cs
│   ├── RandomExplodeController.cs
│   ├── GroundPoundController.cs
│   ├── ExplosiveImpactController.cs
│   └── RagdollController.cs
│
├── Multiplayer
│   ├── ServerQueueController.cs
│   ├── AutoHostController.cs
│   ├── ScreenShareController.cs
│   ├── AvatarCopierController.cs
│   ├── AvatarSearchController.cs
│   ├── PlayerInfoController.cs
│   ├── StareAtPlayerController.cs
│   ├── MapChangeController.cs
│   └── QuickMenuController.cs
│
├── Cosmetics
│   ├── BodyLogColorController.cs
│   ├── HolsterHiderController.cs
│   ├── CosmeticPresetController.cs
│   ├── BodylogPresetController.cs
│   ├── WeepingAngelController.cs
│   └── XYZScaleController.cs
│
├── Filtering
│   ├── BlockController.cs
│   ├── SpawnMenuController.cs
│   ├── DespawnAllController.cs
│   ├── AntiDespawnController.cs
│   ├── ForceSpawnerController.cs
│   └── SpawnableSearcher.cs
│
├── Safety
│   ├── AntiGrabController.cs
│   ├── AntiGravityChangeController.cs
│   ├── AntiKnockoutController.cs
│   ├── UnbreakableGripController.cs
│   ├── FreezePlayerController.cs
│   └── RemoveWindSFXController.cs
│
├── BonelabUtilityUpdater/           # Auto-update plugin
│   ├── UpdaterPlugin.cs
│   ├── AutoUpdater.cs
│   └── BonelabUtilityUpdater.csproj
│
├── StandaloneServerQueue/           # Standalone mods
├── StandaloneRagdoll/
├── StandaloneObjectLauncher/
└── StandaloneBodyLogColor/
```

---

## Building from Source

```
dotnet build BonelabUtilityMod.csproj /p:AssemblyName=BonelabUtilityMod
```

Update the DLL reference paths in `BonelabUtilityMod.csproj` to match your BONELAB installation before building.

Target framework: **.NET 6.0**

---

## Troubleshooting

- **Mod not loading** — Check the MelonLoader console for errors. Ensure MelonLoader and BoneLib are installed.
- **Menu not appearing** — BoneLib must be loaded. Verify it's in your Mods folder.
- **Multiplayer features not working** — LabFusion must be installed and connected to a server.
- **Server queue not detecting full servers** — The queue only triggers on "server full" disconnect messages from Fusion.
- **Updater returning 404** — The GitHub repository must be public for the updater to fetch releases.

---

## License

Free to use and modify for personal use.
