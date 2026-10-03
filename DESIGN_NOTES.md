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
