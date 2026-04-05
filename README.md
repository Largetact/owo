# OwO — Bonelab Utility Mod

> v4.6.0 — A comprehensive MelonLoader utility mod for BONELAB.

---

## Features

### Movement

- **Flight** — Hold both grips + triggers to fly. Speed scales with hand distance from body. Supports acceleration, momentum, lock-on targeting, and custom effects.
- **Dash** — Directional boost with instantaneous or continuous modes. Hand-oriented, lock-on targeting, landing velocity kill.
- **Bunny Hop** — Source Engine-style bunny hopping with air strafing, speed preservation, and auto-hop. Includes surf/trimp system (TF2-style ramp launches), configurable hop boost, max speed, air strafe force, and jump effects.
- **Spinbot** — Auto-rotate the player body at configurable speed.
- **Teleport** — Save/load positions, teleport to players, waypoint system (up to 10 saved slots).
- **Auto Run** — Hold direction to auto-run.

### Player

- **God Mode** — Health stays above 0. You still take damage but cannot die.
- **Ragdoll** — Comprehensive ragdoll physics system with LIMP and ARM_CONTROL modes, tantrum mode, grab detection (head/neck/single-arm/both-arms/body), physics-based triggers (fall/impact/launch/slip/wall-push with individual thresholds), death persistence, VR keybinds (thumbstick press or double-tap B), and external mod override detection.
- **Ragdoll Reload** — Assists magazine insertion while ragdolled in ARM_CONTROL. Detects when a held magazine is near a held gun's ammo socket and bypasses the strict alignment checks that floppy ragdoll arms can't satisfy. Configurable assist distance.
- **Anti-Ragdoll** — Detects and reverses forced ragdolling by other players.
- **Anti-Slowmo** — Blocks forced time-scale changes from other players.
- **Anti-Teleport** — Detects and reverts sudden forced teleportation.
- **Anti-Gravity Change** — Blocks gravity manipulation.
- **Anti-Knockout** — Prevents knockout from damage.
- **Ghost Mode** — Makes you invisible to other players on the network.
- **Force Grab** — Pull items from any distance. Supports instant/global modes, multi-item, player push/pull via Fusion.
- **Default World** — Set a default level to load into automatically on game start.
- **Unbreakable Grip** — Items cannot be dropped once grabbed.
- **Gravity Boots** — Walk on walls and ceilings. Surface detection via raycasts with tunable gravity strength and rotation speed.
- **XYZ Scale** — Independently scale player X/Y/Z dimensions.

### Weapons

- **Gun Modifier** — Glow, insane damage, no recoil, insane fire rate, no weight, bounce, no reload.
- **Full Auto** — Convert any gun to full-auto (60–2000 RPM).
- **Infinite Ammo** — Guns never run out of ammunition.
- **Damage Multiplier** — Scale gun projectile and melee damage independently (0.1x–100x).

### Gun Visuals

- **Custom Gun Color** — Full RGBA color customization for held guns with gradient mode (two-color animated ping-pong along the barrel axis), adjustable gradient speed and spread.
- **Shader Library** — Browse all loaded game shaders, preview by name, apply any shader to held guns, and revert. "Scan All Mod Shaders" loads every shader from all installed mod pallets at once without needing to spawn items. Shows source pallet name and author for each shader. Favorites system to bookmark preferred shaders, text search to filter by name, favorites-only mode. Automatically skips grip/gizmo/grab components to preserve gun handling. Filters hidden/GUI/internal shaders automatically.
- **Texture Editor** — Generate and apply procedural textures to held guns. Modes: Original, Solid color, Gradient, and Noise (Perlin-based). Supports UV scroll animation, configurable noise scale, and per-channel color control.
- **Transparency** — Make guns transparent with adjustable alpha.

### Combat

- **Explosive Punch** — 6 explosion types (Normal, Super, BlackFlash, Tiny, Boom, Custom). Separate left/right hand modes with SmashBone overlay and cosmetic effects.
- **Ground Slam** — Trigger explosions on high-velocity ground impact.
- **Explosive Impact** — Explosions on melee/thrown object hits.
- **Object Launcher** — Fire any spawnable from your hand. Full-auto mode, trajectory preview, multi-projectile grid, homing system with targeting filters, preset management, safety system (grip prevents firing).
- **Random Explode** — Spontaneous explosions at configurable intervals/chance. B+Y controller shortcut.
- **Spawn on Player** — Drop items on/above/below other players with launch force, homing, and count controls.
- **Recoil Ragdoll** — Gun recoil ragdolls the player after a configurable delay. Applies impulse force opposite to barrel direction. Configurable cooldown, force multiplier, and optional gun drop.
- **Aim Assist** — Aimbot with configurable FOV cone, smoothing, and visibility checks. Uses RaycastAll with sorted filtering to ignore projectiles near the muzzle. Properly handles god-mode players.
- **ESP** — Player ESP with skeleton rendering, 3D wireframe box outlines, and tracers. Item ESP with Meteor Client-style beacon beams shooting upward from items. All rendering uses VR-compatible shaders (ZTest=Always). Configurable beam height/width. Auto-hides ESP on held objects.

### Cosmetics

- **BodyLog Color** — Full RGBA customization of Body Log hologram, ball, line, and radial menu colors.
- **Holster Hider** — Hide holstered weapons.
- **Avatar Copier** — Copy another player's avatar, cosmetics, nickname, and description. Revert support.
- **Weeping Angel** — Freeze when observed, move when not.
- **Cosmetic Presets** — Save/load cosmetic configurations.
- **BodyLog Presets** — Save/load color schemes.
- **Disable Avatar FX** — Disable avatar-specific visual effects.

### Server

- **Auto Host** — Automatically hosts a friends-only lobby on Fusion login with exponential backoff retry.
- **Server Queue** — Detects "server full" disconnects and auto-queues. Polls at configurable intervals, rejoins automatically when a slot opens. Works with both code-based and lobby ID-based joins.
- **Freeze Player** — Freeze other players in place.
- **Anti-Grab** — Prevent other players from grabbing you.
- **Screen Share** — Stream desktop or window to other VR players over the network. Configurable scale, FPS (15/30/60), FFmpeg compression, public/LAN IP modes.
- **Player Info** — View player Steam IDs, avatars, and status.
- **Stare at Player** — Make your rig look at another player.
- **Server Settings** — Configure server-side settings when hosting.

### Utility

- **Despawn All** — Clear items with filters (guns, melees, NPCs, network props, etc.) and auto-despawn timer.
- **Anti-Despawn** — Prevent items from being despawned by other players.
- **Spawn Limiter** — Rate-limit item spawning per player (host mode) or globally (client mode). Configurable delay and per-frame cap.
- **Force Spawner** — Spawn items at proximity.
- **Change Map** — Load levels by barcode with level search.
- **AI NPC Controls** — Manipulate NPC mental states, HP, and mass on held or all scene NPCs.
- **Avatar Logger** — Tracks avatar changes across players with optional notifications.
- **Lobby Browser** — Browse and join available Fusion lobbies.
- **Player Action Logger** — Logs player joins, leaves, and deaths with Fusion event hooking.
- **Spawn Logger** — Logs when items are spawned with optional notifications.
- **Auto Updater** — DLL manager that scans Mods/Plugins folders, checks GitHub for updates, auto-downloads new versions, and supports backup/delete/restore of mod DLLs. Configurable check interval and auto-install.

### Blocking & Filtering

- **Player Block** — Despawn items from specific players.
- **Item Block (Server)** — Auto-despawn specific items server-wide.
- **Local Block (Client)** — Hide items locally without network despawn.

### Menus & Controls

- **BoneMenu** — Full mod configuration through BoneLib's BoneMenu system.
- **VR Overlay Menu** — 3D wrist-mounted menu rendered in world space. Palm-up clipboard style with laser-pointer selection. 9 pages (Movement, Player, Weapons, Gun Visuals, Combat, Cosmetics, Server, Utility, Settings). Customizable opacity, accent/background colors, and rainbow mode. Toggle with Grip+X.
- **IMGUI Overlay** — Lightweight desktop GUI overlay with collapsible sections and search.
- **Keybinds** — Rebindable keyboard shortcuts for God Mode, Dash/Flight targeting, Despawn, and menu toggles.

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

The updater plugin (`BonelabUtilityUpdater.dll`) runs before the mod loads and checks GitHub Releases for new versions. If a newer release is found, it downloads the updated DLL and replaces the old one automatically. Supports backup of old DLLs, configurable check intervals, and an "Offline Mode" preference to disable update checks.

---

## Standalone Mods

Individual features are also available as separate, lightweight mods:

| Mod                          | Description                     |
| ---------------------------- | ------------------------------- |
| **StandaloneServerQueue**    | Server queue auto-rejoin system |
| **StandaloneRagdoll**        | Ragdoll physics system          |
| **StandaloneObjectLauncher** | Object launcher                 |
| **StandaloneBodyLogColor**   | Body Log color customization    |
| **StandaloneSpoofing**       | SteamID/Username/Nick spoofing  |

Each standalone mod has its own BoneMenu page and MelonPreferences.

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
