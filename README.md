# Room Icons

A mod for [Book of Hours](https://store.steampowered.com/app/1028310/BOOK_OF_HOURS/)
that draws each locked room's unlock requirements as aspect icons on the map.

Hush House shows a room's unlock cost only when you hover it for a moment, or when you
click into its detail window. Room Icons draws those requirements onto every locked,
reachable room, so you can plan a route through the house at a glance.

## Reading the icons

- **A solid group** is the essential aspects. You need all of them.
- **A dot-separated group** is the required principles. Any one of them, at its listed
  strength, is enough.

Icons scale up as you zoom out, holding their size on screen so the whole house stays
readable from any distance. They disappear once a room is opened.

## Installing

Room Icons is a DLL mod, so it needs **Ghirbi, the Gatekeeper** to load. Subscribe to
Ghirbi on the Steam Workshop first. There is no dependency mechanism that installs it for
you.

Then put the mod folder in the game's local mods directory:

| | |
|---|---|
| Windows | `%USERPROFILE%\AppData\LocalLow\Weather Factory\Book of Hours\mods\` |
| macOS | `~/Library/Application Support/Weather Factory/Book of Hours/mods/` |
| Linux | `~/.config/unity3d/Weather Factory/Book of Hours/mods/` |

The folder is named `room_icons` and holds `synopsis.json` next to a `dll/` directory
containing `RoomIcons.dll` and `0Harmony.dll`. Mods are catalogued at startup, so restart
the game after installing.

## Building from source

You need the .NET SDK and a copy of the game, which supplies the assemblies the mod
compiles against. Harmony comes from NuGet.

```sh
cp Directory.Build.props.example Directory.Build.props
# edit Directory.Build.props to point BohManaged at your own install
dotnet build -c Release
```

That writes `RoomIcons.dll`, `0Harmony.dll` and `THIRD-PARTY-NOTICES.txt` into `dll/`.
Copy `dll/` and `synopsis.json` into a `room_icons` folder in the mods directory above.

Building from WSL against a Windows install has to run the Windows `dotnet.exe`, because
MSBuild cannot follow WSL symlinks and reads `BohManaged` as a native Windows path.

### On the bundled Harmony

The game ships no Harmony of its own, so every DLL mod carries one, and whichever mod
initialises first pins the version for all of them. Room Icons builds against 2.3.6 and
ships the same binary Didumos does, so the two agree whichever loads first. A mod that
works under one load order and not another is usually this.

## License

Room Icons is under [LICENSE](LICENSE).

It also ships `0Harmony.dll`, which is not covered by that. Harmony is MIT, and the
MonoMod merged into the same file is MIT as well. Both notices are in
[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt), which the build copies next to the
binary. Keep it in anything you redistribute, which is what those licenses ask for.
