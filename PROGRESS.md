# Progress

| Phase | Status |
|---|---|
| 1. Foundation | Code done; waiting for first open in the Unity Editor |
| 2. Merge Board | Not started |
| 3. Economy and Base | Not started |
| 4. Battle Core | Not started |
| 5. Strategy Layer | Not started |
| 6. Campaign Content and Tools | Not started |
| 7. Polish | Not started |

## Phase 1: Foundation

### Done
- Folder structure from spec Section 3.
- Assembly definitions: `Game.Core`, `Game.Logic`, `Game.UI`, `Game.Editor`, `Game.Tests` (layout in DESIGN_NOTES.md).
- `Packages/manifest.json` with URP, Input System, uGUI/TextMeshPro, Test Framework, 2D Sprite.
- `GameServices` service root, `EventBus`, `IClock` (`SystemClock`, `ManualClock`), `IGameLog`.
- Save system: local JSON with a version number, migration hook (`ISaveMigration`), backup of the previous save,
  recovery from a corrupt file, protection against saves from a newer build, autosave on change (debounced)
  and on app pause/focus loss/quit.
- `GameConfig` ScriptableObject for shell tunables (save file name, autosave delay, frame rate, debug overlay).
- Boot flow: `GameBootstrap` builds services, loads the save, opens Base. Pressing Play in any scene boots first.
- Editor menu `Tools > Merge Command > Setup Project` (portrait, product name, Input System, URP 2D asset)
  and `Tools > Merge Command > Build Scenes` (Boot, Base, Battle + Build Settings).
- Temporary debug overlay: save status, Save now, Delete save, scene switch.
- 47 EditMode tests (event bus, services, JSON, save round trip, autosave, backup/corrupt/too-new, migrations, disk storage).
  They pass under .NET; GitHub Actions runs them on every push.

### How to try it in the Editor
1. Install Unity Hub and **Unity 6 LTS (6000.0.x)** with Android and iOS Build Support.
2. Clone the repo and check out this branch.
3. Unity Hub > **Add > Add project from disk** > pick the repo folder > open it with Unity 6 LTS.
   Unity creates `ProjectSettings/` and `.meta` files on first open; accept any prompt about the editor version.
4. If the Package Manager reports a version it cannot find, open **Window > Package Manager** and click
   **Update** on that package (versions in `Packages/manifest.json` target 6000.0).
5. Run **Tools > Merge Command > Setup Project**. If Unity asks to restart for the new Input System, choose **Yes**.
6. Run **Tools > Merge Command > Build Scenes**. It creates the scenes and opens Boot.
7. In the Game view, add a **1080x1920** portrait resolution and select it.
8. Press **Play**. The debug overlay shows `Load: NewGame`. Click **Save now**, stop Play, press Play again:
   it now shows `Load: Loaded` and the last saved time. **Delete save** resets to a new game.
9. Run the tests: **Window > General > Test Runner > EditMode > Run All** (47 tests), or from a terminal:
   `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml`
10. Commit what Unity generated (`.meta` files, `ProjectSettings/`, `Packages/packages-lock.json`, the scenes,
    `Assets/_Game/Data/GameConfig.asset`, `Assets/_Game/Settings/`). These cannot be generated outside the Editor.

### Manual URP setup (only if Setup Project logs an error)
1. **Assets > Create > Rendering > URP Asset (with 2D Renderer)**, save it in `Assets/_Game/Settings`.
2. **Edit > Project Settings > Graphics**: set **Default Render Pipeline** to that asset.

### Known issues
- Not yet opened in the Unity Editor: the Unity-side scripts were compile-checked against Unity reference
  assemblies (2021.1) outside the Editor, not against Unity 6 itself.
- Debug overlay uses IMGUI; real UI arrives with Phase 2.
- Android/iOS bundle identifier is not set yet.

## Next: Phase 2, Merge Board
Board model and merge rules, drag-and-drop view, buy button, merge animation hooks.
