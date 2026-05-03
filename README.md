# COI Editor

A focused quality-of-life mod for **Captain of Industry** that adds an in-game editor for resources, settlement values, sandbox options, weather, terrain helpers, and logistics tools.

The project currently ships as the `ResourceQuantityEditor` mod. It can be opened through an in-game UI or driven directly from the developer console with `rqe_*` commands.

## Highlights

- Edit globally available product quantities.
- Search products and apply exact, add, or remove operations.
- Toggle common sandbox-style modifiers in normal saves.
- Adjust settlement population and worker requirements.
- Unlock or finish research.
- Control weather overrides and weather intensity.
- Remove selected trees and process terrain designations.
- Create, capture, and land asteroids with one or two chosen materials.
- Reveal, scan, and visit world-map locations.
- Add repaired cargo ships and increase vehicle limits.
- Open the in-game editor window with **F8** or `rqe_ui`.

## Project Layout

```text
src/
  ResourceQuantityEditor/
    ResourceQuantityEditor.csproj
    ResourceQuantityEditor.cs
    ResourceQuantityEditorUi.cs
    StorageResourceConsoleCommands.cs
    StorageResourceEditorService.cs
    GlobalResourceEditorService.cs
    SandboxFeatureService.cs
    TreeRangeRemovalService.cs
    manifest.json
    config.json
    readme.txt
```

## Requirements

- Captain of Industry installed locally.
- .NET Framework 4.8 targeting pack / MSBuild.
- Game assemblies available under the Captain of Industry game folder.

By default, the project expects the game at:

```text
C:\Games\Captain of Industry\game
```

If your installation is somewhere else, pass `COI_ROOT` to MSBuild.

## Build

From the repository root:

```powershell
msbuild .\src\ResourceQuantityEditor\ResourceQuantityEditor.csproj /p:Configuration=Release /p:COI_ROOT="C:\Path\To\Captain of Industry\game"
```

The project deploys the compiled mod to:

```text
%APPDATA%\Captain of Industry\Mods\ResourceQuantityEditor
```

You can override that destination with:

```powershell
msbuild .\src\ResourceQuantityEditor\ResourceQuantityEditor.csproj /p:Configuration=Release /p:COI_MODS="C:\Path\To\Mods"
```

## Usage

Start a save, open the in-game console, and use the `rqe_*` commands below. The UI can be opened with:

```text
rqe_ui
```

or by pressing **F8** in game.

### Resource Commands

```text
rqe_list_products
rqe_list_products <filter>
rqe_list_global
rqe_list_global <filter>
rqe_set_global <productId> <amount>
rqe_add_global <productId> <amount>
rqe_remove_global <productId> <amount>
```

### Storage Commands

```text
rqe_list_storages
rqe_set_storage <storageId> <productId> <amount>
rqe_add_storage <storageId> <amount>
rqe_remove_storage <storageId> <amount>
rqe_fill_storage <storageId>
rqe_empty_storage <storageId>
```

Use `rqe_list_storages` first to find storage IDs and assigned product IDs.

### Sandbox And Settlement Commands

```text
rqe_sandbox_status
rqe_enable_sources_sinks
rqe_set_sources_sinks <true|false>
rqe_enable_sandbox_cheats
rqe_disable_sandbox_cheats
rqe_set_insta_build <true|false>
rqe_enable_instant_construction <true|false>
rqe_unlock_all_research
rqe_set_population <amount>
rqe_workers_status
rqe_add_workers <amount>
rqe_remove_population <amount>
rqe_ignore_missing_workers <true|false>
rqe_ignore_missing_maintenance <true|false>
rqe_ignore_fuel_consumption <true|false>
rqe_ignore_missing_power <true|false>
rqe_ignore_missing_computing <true|false>
rqe_ignore_missing_unity <true|false>
rqe_ignore_missing_food <true|false>
rqe_unlimited_food <true|false>
rqe_unlimited_unity <true|false>
rqe_unlimited_vehicle_fuel <true|false>
```

### Environment, Terrain, Weather, And Logistics

```text
rqe_no_bio_waste <true|false>
rqe_no_landfill_waste <true|false>
rqe_no_toxic_waste <true|false>
rqe_no_radioactive_waste <true|false>
rqe_remove_selected_trees
rqe_list_weather
rqe_list_weather <filter>
rqe_set_weather <weatherId>
rqe_clear_weather
rqe_toggle_sunny_weather
rqe_add_repaired_cargo_ship
```

## Configuration

`config.json` exposes the mod settings used by the game:

- `allow_replace_non_empty_storage` controls whether `rqe_set_storage` can replace the assigned product in a non-empty storage.
- `max_single_operation_amount` caps the amount accepted by one storage editor command.

## Notes

This mod directly changes save-game state. Make a backup before using destructive operations such as removing resources, replacing storage contents, clearing terrain designations, or changing world-map progression.

## Repository

[github.com/Ravva/coi-editor](https://github.com/Ravva/coi-editor)
