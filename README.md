A mod for [Book of Hours](https://store.steampowered.com/app/1028310/BOOK_OF_HOURS/)
that draws each locked room's unlock requirements as aspect icons on the room itself.

## Installing

Room Icons is a DLL mod, so it needs **Ghirbi, the Gatekeeper** to load. Subscribe to
Ghirbi on the Steam Workshop first. 

Install with [Steam workshop]() or put the extracted release folder in the game's local mods directory:

| | |
|---|---|
| Windows | `%USERPROFILE%\AppData\LocalLow\Weather Factory\Book of Hours\mods\` |
| macOS | `~/Library/Application Support/Weather Factory/Book of Hours/mods/` |
| Linux | `~/.config/unity3d/Weather Factory/Book of Hours/mods/` |

## Building from source

You need the .NET SDK and a copy of the game, which supplies the assemblies the mod
compiles against. Harmony comes from NuGet or is provided in the distributed release (2.3.6).

```sh
cp Directory.Build.props.example Directory.Build.props
# edit Directory.Build.props to point BohManaged at your own install
dotnet build -c Release
```

Copy `dll/` and `synopsis.json` into a `room_icons` folder in the mods directory above.
