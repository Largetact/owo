# Bonelab Utility Mod

A MelonLoader mod for Bonelab that adds utility features including flight, noclip, and an object launcher.

## Features

- **Flight Mode**: Fly freely using Quest 2 controller thumbsticks
- **Noclip Mode**: Walk through walls and objects
- **Object Launcher**: Pick up and launch objects hand-tracked with Quest 2 controllers
- **God Mode**: Take damage but cannot die - health stays above 0
- **BoneLib Menu Integration**: Customize settings in-game

## Requirements

- MelonLoader v0.6.5
- BoneLib v3.2.1
- .NET Framework 4.7.2
- Visual Studio 2022 (or MSBuild tools)

## Project Structure

```
BonelabUtilityMod/
├── BonelabUtility.cs          # Main MelonMod class
├── FlightController.cs         # Flight feature
├── NoclipController.cs         # Noclip feature
├── ObjectLauncherController.cs # Object launcher feature
├── GodModeController.cs        # God mode feature
├── MenuIntegration.cs          # BoneLib menu setup
└── BonelabUtilityMod.csproj   # Project file
```

## Setup Instructions

1. **Update References in .csproj**
   - Open `BonelabUtilityMod.csproj`
   - Update the hint paths to match your Bonelab installation location
   - Default path: `C:\Program Files (x86)\Steam\steamapps\common\BONELAB`

2. **Build the Project**

   ```
   dotnet build -c Release
   ```

3. **Install the Mod**
   - Copy `bin/Release/net472/BonelabUtilityMod.dll` to your Bonelab `Mods` folder
   - Path: `[Bonelab]/Mods/BonelabUtilityMod.dll`

4. **Launch Bonelab**
   - MelonLoader will automatically load the mod on startup

## Usage

### Flight Controls (Quest 2)

- **Toggle**: Right Controller Menu Button (or use BoneLib menu)
- **Move Forward/Back & Strafe**: Left Controller Thumbstick (Y/X axis)
- **Move Up/Down**: Right Controller Thumbstick (Y axis)
- **Speed Control**: Adjust "Flight Speed" in menu (0.1 - 20)
- **Rotation**: Natural head tracking from Quest 2 headset

### Noclip Controls (Quest 2)

- **Toggle**: Left Controller Primary Button (X button) (or use BoneLib menu)

### Object Launcher Controls (Quest 2)

- **Toggle**: BoneLib menu only (no controller shortcut)
- **Copy Barcode**: Grab object with either hand (Left or Right Grip) - copies the barcode from that object
- **Launch**: Press Trigger on aiming hand to fire projectiles
- **Switch Aim Hand**: Press Primary Button (X/A) on aiming hand to switch between left/right hand aiming
- **Projectile Count**: Adjust in menu (1 - 10) to fire multiple objects at once
- **Launch Force**: Adjust in menu (1 - 200)

**How to Use:**

1. Enable "Object Launcher" in BoneLib menu
2. Pick up an object with your left or right hand (physics objects, items from environment)
3. Grab it with the Grip button to copy its warehouse barcode
4. Point your aiming hand (default: left) and press Trigger to spawn and launch that object
5. Press Primary Button to switch between left and right hand aiming
6. Adjust projectile count in menu to fire multiple objects
7. Faster hand movement = faster projectiles (velocity-based launch)

## Menu Access

Press the BoneLib menu button to access:

- Toggle Flight, Noclip, God Mode, and Object Launcher
- Adjust Flight Speed (0.1 - 20)
- Adjust Launch Force (1 - 200)
- Adjust Projectile Count (1 - 10)

## Important Notes

1. **SteamVR Integration**: This mod uses SteamVR actions for Quest 2 controller input. Ensure SteamVR is properly installed and configured.

2. **God Mode**: Prevents death by keeping health above 0. You still take damage and can be hurt, but won't die. Works by intercepting health drops and restoring minimum health.

3. **Barcode System**: The launcher spawns objects from Bonelab's warehouse using barcodes. Grab any physics object with your hand and press Grip to copy its barcode, then use Trigger to fire copies of that object.

4. **Multi-Projectile**: Set "Projectile Count" in the menu to fire multiple objects in a spread pattern simultaneously.

5. **Hand Switching**: Press your aiming hand's Primary Button (X/A) to switch between left and right hand aiming.

6. **Velocity Tracking**: Faster hand movements result in faster projectile launches (hand velocity is added to base launch force).

7. **Hand Rig Names**: The mod searches for "LeftHand" and "RightHand" GameObjects. If your setup uses different names, update the search in `ObjectLauncherController.Initialize()`.

8. **Player References**: The mod searches for "Player" GameObject. If your Bonelab setup uses a different name, update the search in each controller's `Initialize()` method.

## Troubleshooting

- **Mod not loading**: Check MelonLoader console for error messages
- **References not found**: Verify paths in .csproj match your Bonelab installation (especially SteamVR DLLs)
- **Menu not appearing**: Ensure BoneLib is properly installed and loaded
- **Controllers not responding**: Verify SteamVR is running and controllers are tracked
- **Objects not spawning**: Check that you've copied a barcode (grab an object with Grip button first)
- **Barcode copy failing**: The object must be a physics object with a valid warehouse barcode
- **Projectiles not launching**: Ensure launcher is enabled in BoneLib menu and you have a barcode copied

## Building from Source

1. Clone or download this project
2. Open in Visual Studio or VS Code
3. Update .csproj paths to your Bonelab installation
4. Build Release: `dotnet build -c Release`
5. Copy DLL to Mods folder
6. Launch Bonelab

## License

Free to use and modify for personal use.
