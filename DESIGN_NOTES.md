# Design Notes

Balance decisions and deviations from the build prompt, with reasons.

## Phase 1

### Assembly layout
| Assembly | Folders | Engine? | Contents |
|---|---|---|---|
| `Game.Core` | `Scripts/Core` (except `Core/Unity`) | No (`noEngineReferences`) | Service root, event bus, clock, log, JSON, save system |
| `Game.Logic` | `Scripts/Merge`, `Economy`, `Base`, `Battle`, `Campaign` (via `.asmref`) | No | All game rules |
| `Game.UI` | `Scripts/UI`, `Scripts/Data`, `Scripts/Core/Unity` (via `.asmref`) | Yes | MonoBehaviours, ScriptableObject definitions, bootstrap |
| `Game.Editor` | `Editor` | Editor only | Scene builder, project setup, later validators and level editor |
| `Game.Tests` | `Tests/EditMode` | Editor only | NUnit tests of Core and Logic |

The spec puts bootstrap under `Core/` and ScriptableObject definitions under `Data/`, but both need UnityEngine.
To keep `Game.Core` and `Game.Logic` engine-free (so they compile and test as plain C#), those two folders
join `Game.UI` through `.asmref` files instead of adding more assemblies. `Game.UI` is effectively "the Unity layer".
Namespaces: `Game.Core.*`, `Game.Logic.*`, `Game.UI`, `Game.Data`, `Game.App` (bootstrap), `Game.Editor`, `Game.Tests`.

### Own JSON instead of JsonUtility or Newtonsoft
`JsonUtility` needs UnityEngine and cannot serialize dictionaries; Newtonsoft is not on the allowed package list.
A small JSON model (`Game.Core.Json`, about 500 lines) keeps the save system engine-free and lets migrations
reshape the raw JSON tree before any typed mapping. Numbers keep their original text, so large gold values
round-trip exactly.

### Save format
```json
{ "version": 1, "createdAt": 0, "savedAt": 0, "sections": { "<system key>": { } } }
```
- Each system implements `ISaveSection` (key, `Capture`, `Restore`) and registers in `GameBootstrap` before load.
- Sections in the file that no system claims are written back unchanged, so data is never silently dropped.
- Every save first copies the previous good file to `save.bak.json`. If the main file is unreadable the backup is used;
  if both are unreadable a new game starts and the bad file is kept as `save.corrupt.json`.
- A save from a newer build is never overwritten: the service goes read-only for that session.
- Autosave writes `autosaveDelaySeconds` (default 2) after the *first* unsaved change, so constant activity cannot
  postpone a save indefinitely. Pause, focus loss and quit flush immediately.
- `SaveFormat.CurrentVersion` is a code constant, not config, because it must match the registered migrations.

### Running tests outside Unity
The Unity Editor cannot run in the cloud environment used to build this. `tools/LogicTests/LogicTests.csproj`
compiles `Game.Core`, `Game.Logic` and the EditMode tests with the .NET SDK and runs them with NUnit 3 at C# 9
(Unity's language version). The same test files run unchanged in Unity's Test Runner. CI runs this on every push.

### Not committed until the first Editor open
`.meta` files, `ProjectSettings/`, scenes and `.asset` files are created by Unity and the editor scripts,
not written by hand (spec Section 2). They get committed after the first open (PROGRESS.md step 10).

## Phase 2

### Merge rules
- Same type and level merges into level + 1 at the **target** slot; the source slot empties.
- Same type at a different level, or a different type, swaps. An empty target moves.
- Two identical items already at max level: nothing changes and a message says "Already at max level"
  (a swap of identical items would look like a bug).
- Buying checks for space **before** charging, so a full board never costs gold or loses an item.
- The board only grows (4x5 to 6x7). A save with items outside the stored size grows the board to fit, and two items
  stored in one cell both survive. Items of a type removed from the config are the only thing dropped on load.

### First-pass prices (MergeConfig asset)
- Cost = `baseCost × 2^(level−1) × 1.07^purchases`. The level factor of 2 means a level-N purchase costs about the
  same as the 2^(N−1) level-1 items it replaces. The 1.07 growth is per type, so buying one type does not raise the
  others' prices.
- Base costs: Infantry 100, Artillery 120, Armor 150, Gold Mine 200, Warehouse 400, Barracks 500.
- Buy level = highest level of that type ever owned − 5, minimum 1 (spec example "N−5").
- Max level 30 for every type. All of these live in the asset, not in code.

### Temporary stand-ins
- Phase 3 replaced the `UnlimitedWallet` with the real wallet and the debug board-growth button with HQ upgrades.
- Text uses legacy uGUI `Text` with the built-in font. TextMeshPro needs its Essentials imported in the Editor
  before runtime-created text renders; switching to TMP is planned with localization in Phase 7 (needed for Arabic).
- `Strings` is a small in-code English table. Every UI string already goes through it by key, so Phase 7 can swap
  in a localization asset without touching call sites.
- Board placement uses a time-seeded `DeterministicRandom`; placement does not need to be replayable.

### Compile check outside the Editor
Unity-side code is compiled against UnityEngine/UnityEditor 2021.1 and UnityEngine.UI 2020.3 reference assemblies
(NuGet `Unity3D.SDK`, `Unity3D.UnityEngine.UI`), with generated facade assemblies for the UnityEngine modules.
It catches API and type errors, but not Unity 6-specific API changes.

## Phase 3

### Economy numbers (EconomyConfig asset)
- **Gold Mine:** 1 gold/s at L1, ×2.1 per level. Merging two mines (2 × L1 = 2/s) gives one L2 at 2.1/s and frees a
  slot, so merging always pays a little.
- **Barracks:** 2% off unit prices per Barracks level, summed across all Barracks, capped at 50%. Buildings are never discounted.
- **Warehouse:** the offline cap is 8 h + 0.5 h per Warehouse level, capped at 24 h.
- **Offline rate:** 100% of normal income (the multiplier is in the asset if you want less).
- **Starting balance:** 500 gold, which is enough for a Gold Mine plus a few units. 0 gems; gems come from stars later.
- **HQ:** 10 levels, each costing 5× the previous (2,000 for level 2, up to 781 M for level 10). Levels 2 to 5 grow the board
  (5x5, 5x6, 6x6, 6x7). Every level raises the highest buy level, from L5 to L30. Trait unlocks per HQ level have a
  slot in the data, filled in Phase 5.

### Time away
- Income is paid in `EconomyService.Tick(now)`, called every frame. A gap between ticks longer than 60 s counts as time
  away. The same rule covers a cold start (the last active time is saved) and coming back from the background, so there
  is one code path to test.
- Offline gold is added immediately. The popup only reports it, so closing the app before tapping Collect loses nothing.
- If the clock moves backwards (the device time was changed), nothing is paid and the game carries on from the new time.
- `OffsetClock` lets the debug panel fake time away without changing the device clock.
