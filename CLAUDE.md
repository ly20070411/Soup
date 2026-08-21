# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity 2022.3.12f1 2D turn-based cooking-management roguelite (“赣什么” / Soup). All gameplay code lives in `Assets/Scripts` under the `Soup.*` namespace (Game, Jobs, Items, Employees, Events, Relics, Levels). Comments and in-game/UI strings are in Simplified Chinese — keep new ones consistent. The design doc is `Assets/Docs/“赣什么”游戏.xlsx`; finished art is dropped into `Assets/Docs/美术素材/完成后上传` and wired up via `Soup/Art Assets/Link Completed Icons`.

## Build & Run

- Open in Unity Editor 2022.3.12f1 (Rider is the configured script editor). There is a single scene, `Assets/Scenes/SampleScene.unity`; nearly all gameplay bootstraps from code at runtime, not from scene objects.
- There are no tests and no CLI build/test scripts (the test-framework package is installed but no test assemblies exist).
- Note: `Packages/manifest.json` references `com.coplaydev.unity-mcp` as `file:MCPForUnity`, an embedded package folder that is not tracked in git. If it's missing on a fresh clone, remove that dependency line or restore the folder.

## Content Pipeline (Editor seeders)

ScriptableObject databases live in `Assets/Resources` (`GameConfig`, `IngredientDatabase`, `JobDatabase`, `RelicDatabase`, `EmployeeDatabase`, `EventDatabase`, `LevelDatabase`); item assets live in `Assets/Data/{Ingredients,Jobs,Relics}`. Content is authored in C# editor seeders under each module's `Editor/` folder and applied through Unity menu items:

- `Soup/Ingredient Manager/Seed Sample Ingredients`
- `Soup/Job Manager/Seed Sample Jobs`, `Soup/Job Manager/Link Gather Jobs By Ingredient Name`
- `Soup/Employee Manager/Seed Employees`
- `Soup/Event Manager/Seed Sample Events`
- `Soup/Relic Manager/Seed All Relics` / `Seed Sample Relics`

To change game content (ingredients, jobs, relics), edit the seeder code and re-run the menu item — the seeders create-or-update assets in place.

## Architecture

### Bootstrap: self-creating singletons

Every manager is a `MonoBehaviour` singleton created by an `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]` `EnsureExists()` (or `Initialize()`), with `DontDestroyOnLoad`. Init order is pinned via `[DefaultExecutionOrder]` and must be respected when adding managers:

MainMenuUI (-110) → RelicManager/JobManager/IngredientManager (-100) → EmployeeManager/JobProgressionManager (-95) → ElfManager (-90) → ResourceStore (-80) → TurnManager (-70) → JobModifierManager (-65) → LevelManager (-60) → EventManager (-55) → JobWorldMap (-50)

### Data access pattern

Runtime systems never read databases directly; they query the wrapper manager (`IngredientManager`, `JobManager`, `RelicManager`, ...), each holding one database asset with an id→item index (`MarkDirty()`/`RebuildIndex()`; ids default from display names and must be unique).

### Turn pipeline (`TurnManager.NextTurn`)

The heart of the game — one production turn resolves strictly in this order:

1. `RelicEffectRunner.Run(TurnStart)`
2. **Gather** (uncapped adds; per-station output tracked in `GatherTurnOutput`)
3. `Run(AfterGather)`
4. **Process** — jobs sorted by `ProcessPriority` desc (explosion/Any-material jobs last among equals); specialized stations consume preferred material first, then others at `OtherMaterialEfficiency`; employees may eat part of the output
5. Warehouse overflow discard — removes this-turn gather output from the highest-numbered station first, materials Solid → Tough → Soft; flavors are never discarded
6. **Cold** flavor (`FlavorResolver.ResolveCold`)
7. **Cook** — consumes processed food for cook score
8. `Run(BeforeSpicy)` → **Spicy** multiplier (capped by `GameConfig.SpicyMultiplierCap`, relics can lift the cap)
9. **Magic** flavor
10. `Run(AfterScore)` → final/independent multipliers

**Sour** flavor is NOT resolved per turn; it converts to score only at stage (大关) settlement in `TurnManager.SettleStage()`.

### Relics: declarative rules

`RelicItem` holds a list of `RelicRule` (trigger + condition + effect with shared parameters) evaluated by the static `RelicEffectRunner` at fixed pipeline points. Add relic behavior by extending `RelicTrigger`/`RelicConditionType`/`RelicEffectType` and handling it in the runner, not by writing bespoke per-relic code.

### Labor model

`EmployeeManager.GetLaborByJob()` maps `JobItem → labor` (fallback: `ElfManager.GetAssignments()`). `ElfManager` is only a compatibility facade over `EmployeeManager` for the default 小精灵 pool. `JobModifierManager` applies event-driven per-job modifiers (disable, yield multiplier, bonus flavor).

### Undo / snapshots

`GameSaveService.Capture()/Apply()` in `GameSaveData.cs` snapshots the full in-run state (resources, turn counters, employees, relics, job progression) across all managers. `TurnManager` captures before every `NextTurn` for the 撤回上一回合 (undo) feature; new run state must be added to both capture and apply.

### UI

Runtime UI is IMGUI (`OnGUI`) — `MainMenuUI` (title + new-run setup picks), `GamePlayHud` (F1 debug/control panel), `JobWorldMap` (elf assignment). Editor tooling follows the same IMGUI window pattern.
