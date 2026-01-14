# GTAStunting Mod

A mod for GTA V designed for stunting.

## Features

### Teleporter
- Save & Load positions with full vehicle state preservation
- **Speed Teleport**: 
    1. Standard teleport without velocity/momentum
    2. Preserve velocity/momentum on load
- **Second Vehicle System**: Save a secondary vehicle position for setups (e.g., positioning a ramp buggy)
- **Ghost Driver**: Record & replay a second vehicle's path for bump stunts (e.g., driving off a moving vehicle)

### Speed Tracking & Analysis
<img src="https://media.discordapp.net/attachments/176774410206838784/1459871087180648564/image.png?ex=69682626&is=6966d4a6&hm=706bf1a322d20b3cf1302f0d5a6309825eebc720b153fb28abc122c4886fd906&=&format=webp&quality=lossless&width=1240&height=830" width="500"/>

- 3 modes:
    1. Ghost lines
    2. Real-time speed graph overlay (km/h)
    3. Ghost lines + Graph overlay
- Record up to 3 ghost attempts for comparison
- **3D Ghost Trails**: Visualize saved attempt paths in-world (ghost lines)
- **Input Markers**: Colored squares show wheelie/stoppie inputs (cyan = wheelie, red = stoppie)
- Export/Import ghost data to CSV for sharing or re-loading for later use (can be reloaded with F8)

### Vehicle Tools
- **Vehicle Spawner**: Quick-spawn any vehicle, fully upgraded - use https://wiki.rage.mp/wiki/Vehicles 
- **Vehicle Config Save/Load**: Save colors, mods, extras to XML files - export with F10, import with F11
- **Taxi Boost**: Vice City-style taxiboost for supported vehicles (Taxi, Dynasty, Eudora, Broadway)

### Utility Features
- **Jetpack Mode**: Noclip-style movement for positioning
- **Linear Steering**: Override keyboard steering with configurable ramp time (VERY VERY BUGGY) - DO NOT USE ATM
- Toggle Invulnerability, Unnoticeability
- Toggle Ghost Town (no peds/traffic)
- Freeze Time & Weather, Cycle Weather

## Default Controls

| Key | Action |
|-----|--------|
| **Y** | Save Position |
| **E** | Load Position |
| **]** | Save Position (with speed) |
| **[** | Load Position (with speed) |
| **N** | Save Second Vehicle |
| **M** | Save Second Vehicle Position |
| **T** | Toggle Ghost Recording |
| **H** | Spawn Vehicle |
| **G** | Toggle Speed Graph |
| **B** | Toggle Jetpack |
| **K** | Toggle Unnoticeability |
| **L** | Toggle Invulnerability |
| **I** | Toggle Time Freeze |
| **O** | Toggle Ghost Town |
| **U** | Cycle Weather |
| **F5** | Export Ghost Data to CSV |
| **F6** | Clear All Attempts |
| **F8** | Save Last Attempt as Ghost |
| **F9** | Import Ghosts from CSV |
| **F10** | Save Vehicle Config |
| **F11** | Load Vehicle Config |
| **F12** | Toggle Linear Steering |

All controls are configurable via `GTAStunting.ini`.

## Installation

1. Install [ScriptHookV](http://www.dev-c.com/gtav/scripthookv/)
2. Install [ScriptHookVDotNet **NIGHTLY**](https://github.com/scripthookvdotnet/scripthookvdotnet-nightly/releases)
3. Copy `GTAStunting.dll` and `GTAStunting.ini` to your `scripts` folder

## Configuration

Edit `GTAStunting.ini` to customize controls and defaults.

## Data Files

- **Vehicle Configs**: `scripts/VehicleConfigs/*.xml`
- **Speed Data Exports**: `scripts/stunt_ghosts_*.csv`
    - Supports user defined .csv files, so can be named anything.
## Source Code

The source is provided for customization. Built with:
- ScriptHookVDotNet 3.x
- .NET Framework 4.8

Feel free to modify features, change keybinds, or add functionality to suit your stunting workflow.

## Credits
- **Darkstar** for the initial development of this mod.
- **JeriChopper** for testing.
- **You** for stunting in 2026.
- **Dannye**: Dannye's Ultimate Stunter's SCM for VC/SA is the inspiration for this mod.

## TODO

- demonstration video

## License

MIT License - Use freely, modify as needed.
