# UndertaleModTool WinUI Preview

A personal experimental WinUI 3 fork of [UndertaleModTool](https://github.com/UnderminersTeam/UndertaleModTool), focused on a modern Windows editing experience for GameMaker data files.

This is not an official replacement for upstream UndertaleModTool. It is a Windows-only fork that keeps the original project, license, and credits intact while exploring a different UI direction.

## Status

This fork is a preview. Expect rough edges.

The goal is to make common editing and inspection workflows easier on Windows without creating maintenance pressure for upstream.

## Why This Fork Exists

The classic UndertaleModTool UI is powerful, but dense. This fork experiments with a more direct Windows-native shell for browsing, previewing, and editing resources.

The underlying purpose is still the same: inspect, mod, decompile, edit, and repack Undertale, Deltarune, and other GameMaker data files.

## Improvements

* WinUI 3 interface with a flatter, more native Windows layout.
* Cleaner resource browsing with category counts, filtering, tabs, and calmer navigation.
* Richer sprite and texture previews with zoom controls, frame navigation, larger preview windows, and export actions near the preview.
* Embedded texture atlas interaction, including selecting texture page items directly from atlas previews.
* In-app playback for embedded audio and sound resources.
* More visible audio metadata, including format, byte size, linked embedded audio ID, volume, pitch, preload, and group information.
* Less eager rendering for heavy image, sprite, room, and texture previews.
* Preview caching for faster repeated preview and export workflows.
* Cleaner empty state and recent-file flow.
* Script command entry for quick C# script commands.
* More restrained Windows UX styling: calmer selection states, better spacing, and less noisy visual contrast.

## Scope

This fork is Windows-only because it uses WinUI 3 and the Windows App SDK.

Cross-platform work belongs in a separate Avalonia, Uno, or other cross-platform UI direction. This fork is deliberately the Windows experiment.

## Relationship To Upstream

This repository is based on [UnderminersTeam/UndertaleModTool](https://github.com/UnderminersTeam/UndertaleModTool).

Original upstream work, contributors, scripts, data format research, and license terms remain credited and preserved. This fork is not maintained by the Underminers team unless explicitly stated upstream.

## Quick Start

### Run From Source

Requirements:

* Windows 10 1809 or newer, or Windows 11.
* .NET 10 SDK or newer.
* Git submodules initialized.

Clone with submodules:

```powershell
git clone --recurse-submodules https://github.com/cmdr-chara/UndertaleModTool.git
cd UndertaleModTool
git switch winui-preview
```

Run the WinUI preview:

```powershell
dotnet run --project .\UndertaleModTool.WinUI\UndertaleModTool.WinUI.csproj -c Debug -p:LangVersion=latest
```

Build the WinUI preview:

```powershell
dotnet build .\UndertaleModTool.WinUI\UndertaleModTool.WinUI.csproj -c Debug -p:LangVersion=latest
```

## Opening GameMaker Data Files

Use **Open data file** and select a supported GameMaker data file, such as:

* `data.win`
* `game.ios`
* `game.unx`
* `game.droid`

Always keep a backup of the original file before saving changes. Tools do not need malice to ruin your evening; a bug is enough.

## Included Projects

Important projects in this fork:

* `UndertaleModTool.WinUI` - the experimental WinUI 3 interface.
* `UndertaleModTool` - the original WPF interface from upstream.
* `UndertaleModCli` - command-line tooling.
* `UndertaleModLib` - shared core library for GameMaker data files.
* `Underanalyzer` - submodule used for analysis and decompiler work.

## Original Features

The upstream tool supports:

* Reading and writing GameMaker data files.
* Recreating decoded data back into valid game data files.
* Editing many known and unknown resource values.
* GML VM code editing.
* High-level GML decompilation and compilation.
* Script-based automation.
* Room and level editing.
* Core library usage from external tools.

For detailed data format information, see the upstream [UndertaleModTool wiki](https://github.com/UnderminersTeam/UndertaleModTool/wiki).

## License

This fork preserves the upstream license. See [LICENSE.txt](LICENSE.txt).

## Credits

Original UndertaleModTool project:

* [UnderminersTeam/UndertaleModTool](https://github.com/UnderminersTeam/UndertaleModTool)
* [UndertaleModTool contributors](https://github.com/UnderminersTeam/UndertaleModTool/graphs/contributors)

Research and related projects credited by upstream:

* [PoroCYon's UNDERTALE decompilation research, maintained by Tomat](https://tomat.dev/undertale)
* [Donkeybonks's GameMaker data.win bytecode research](https://web.archive.org/web/20191126144953if_/https://github.com/donkeybonks/acolyte/wiki/Bytecode)
* [PoroCYon's Altar.NET](https://github.com/PoroCYon/Altar.NET)
* [WarlockD's GMdsam](https://github.com/WarlockD/GMdsam)
