# Armoryeet development guide

Practical notes for returning to this repository.

## Toolchain

- .NET SDK: `10.0.301`.
- Dalamud SDK: `Dalamud.NET.Sdk/15.0.0`, restored from NuGet.
- Target: supplied by `Dalamud.NET.Sdk`; the model-check project targets `net10.0-windows7.0`.
- NuGet source: `https://api.nuget.org/v3/index.json`.
- Runtime testing: FFXIV with XIVLauncher/Dalamud and a local Dalamud dev-plugin workflow.

`global.json` pins the SDK. The project does not bundle the .NET SDK. A newer SDK may not satisfy the pin because roll-forward is limited to the latest patch in the selected feature band.

Verify the machine:

```bash
dotnet --version
dotnet --list-sdks
```

Expected SDK output includes `10.0.301`.

## Build and checks

From the repository root:

```bash
dotnet restore src/Armoryeet/Armoryeet.csproj
dotnet build src/Armoryeet/Armoryeet.csproj -c Release
dotnet run --project tests/Armoryeet.ModelTests/Armoryeet.ModelTests.csproj
```

The model project is a console assertion harness, not an xUnit/NUnit test project. `dotnet test` does not execute its assertions. A successful run prints `Armoryeet model tests passed.`

Useful checks:

```bash
git status --short
git diff --check
jq empty repo.json src/Armoryeet/Armoryeet.json
```

Restore/build output is ignored under `bin/`, `obj/`, and `artifacts/`.

## Repository layout

The primary plugin project is [src/Armoryeet/Armoryeet.csproj](src/Armoryeet/Armoryeet.csproj). Its manifest template is [src/Armoryeet/Armoryeet.json](src/Armoryeet/Armoryeet.json). `DalamudPackager` fills generated fields such as the internal name, assembly version, and API level.

The entrypoint is `Plugin`:

- registers commands and UI callbacks;
- constructs scanners, the mover, and main and settings windows;
- owns `WindowSystem` lifecycle;
- unsubscribes events and disposes the mover/windows on unload.

The runtime path is:

```text
Plugin
  -> GearsetScanner + ArmouryScanner
  -> PluginUI / ScanSession
  -> ItemMover
  -> framework update -> final slot/item/gearset checks -> move
```

Responsibilities:

- `GearsetScanner`: gathers normalized IDs from saved gearsets and equipped items.
- `ArmouryScanner`: reads Armoury containers and Lumina item metadata; returns movable and protected counts.
- `InventoryItemReader`: the single item-ID normalization/read boundary for client structs.
- `ScanSession`: pure selection, grouping, capacity, and reconciliation state.
- `ItemMover`: framework-thread movement state machine, blockers, retries, and skip reasons.
- `ItemMoverSnapshot`: immutable UI-facing movement state.
- `PluginUI` and `ConfigUI`: Diktat presentation and user actions.
- The inline Appearance tab and the plugin-owned `ThemeEditor`: live color preview with explicit Apply/Cancel.
- `vendor/Diktat`: reproducible MIT-licensed library snapshot, built and packaged as Diktat.dll.

Keep game-memory access in the scanner/mover boundary. Keep selection and display policy in the model/UI layers. Do not duplicate item-ID normalization or move safety checks in UI code.

## Safety invariants

Preserve these when changing behavior:

1. Soul crystals never enter the normal scan scope and are always skipped if enqueued.
2. An item is movable only when it is not equipped and not referenced by any saved gearset.
3. Before every move, re-read the Armoury slot and compare the normalized item ID with the reviewed item.
4. Before every move, re-check gearsets/equipped items, login state, movement blockers, and destination capacity.
5. Reviewed moves must revalidate the selected snapshot and capacity at confirmation time.
6. Move attempts are delayed and bounded. A failed/stale item is skipped with a reason; it is not silently retried forever.
7. Only completed items are removed from the reviewed session. Skipped items remain reviewable.
8. Dispose every framework/UI subscription before releasing owned services.

Changes touching `ItemMover`, `GearsetScanner`, `InventoryItemReader`, or confirmation flow need focused model tests plus in-game verification.

## Development workflow

1. Read the relevant owner class before editing.
2. Make the smallest change at the layer that owns the invariant.
3. Run the model harness and Release build.
4. Load the resulting plugin through the local Dalamud dev-plugin workflow.
5. Test with an empty slot, full inventory, an equipped item, a gearset item, a changed Armoury slot, combat/duty state, and stop/retry paths as applicable.
6. Inspect `/xllog` and Dalamud Plugin Stats (`/xldev`) for load errors or per-frame cost.

Avoid direct game-memory work from UI draw code. Framework callbacks and game data access must remain on the thread/context expected by Dalamud and ClientStructs.

## Official references

- [Dalamud Getting Started](https://dalamud.dev/plugin-development/getting-started)
- [Project Layout and Configuration](https://dalamud.dev/plugin-development/project-layout)
- [Setting Plugin Metadata](https://dalamud.dev/plugin-development/plugin-metadata)
- [Calling the Game's Code](https://dalamud.dev/plugin-development/interaction/calling-game-code)
- [Plugin Technical Considerations](https://dalamud.dev/plugin-development/technical-considerations)
- [Dalamud API 15 reference](https://dalamud.dev/api/)
- [SamplePlugin template](https://github.com/goatcorp/SamplePlugin)
- [Custom repository format](https://dalamud.dev/plugin-publishing/custom-repositories)

Docs change. Re-check the API 15 site and the SamplePlugin template when upgrading Dalamud API or SDK versions.
