# Progress

| Phase | Status |
|---|---|
| 1. Foundation | Code done; waiting for first open in the Unity Editor |
| 2. Merge Board | Code done; waiting for an Editor play test |
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

## Phase 2: Merge Board

### Done
- `BoardModel` (plain C#): grid of slots, move / swap / merge rules, max level per type, random empty slot,
  growing the board without ever dropping items.
- `MergeBoardService`: buy into a random empty slot, price rising per purchase of each type, higher buy levels unlocked
  as you merge (highest level − 5), board full and not-enough-gold refusals that charge nothing, board growth up to
  6x7, saving under the `board` section. Publishes `ItemMerged`, `ItemBought`, `BuyFailed`, `ItemDragged`, `BoardChanged`.
- `MergeConfig` asset (`Assets/_Game/Data/MergeConfig.asset`, created by Build Scenes): board sizes, buy-level offset,
  six item types (Infantry, Artillery, Armor, Gold Mine, Barracks, Warehouse) with prices, colors, labels and an
  optional sprite each.
- `DeterministicRandom` (seeded xorshift64*), reused by the battle simulation later.
- Base screen built from code: currency bar, merge board with drag and drop and a highlighted drop target,
  one buy button per type showing name, buy level and price, Battle button, message toast. Safe-area aware,
  1080x1920 reference, touch targets at least 132 px.
- Feedback hooks: merge punch animation, buy pop-in, and sound hooks (`IAudioService`, silent until Phase 7).
- `Tools > Merge Command > Validate Data` checks the merge config.
- Debug overlay (tap "▶ Debug"): Grow board button to test expansion.
- 86 tests in total; 39 new for merge rules and edge cases (full board, max level, invalid drags, prices,
  buy level, save round trip, damaged save data, RNG determinism).

### How to try it in the Editor
1. Do Phase 1 steps 1 to 7 if you have not yet. If you already did, run **Tools > Merge Command > Build Scenes**
   again: it adds the MergeConfig asset and rebuilds the Base scene.
2. Press **Play**. The Base screen shows a 4x5 board and six buy buttons.
3. Tap **Infantry** twice, then drag one Infantry onto the other: they merge into L2 with a bounce.
4. Drag an item to an empty slot (move) and onto a different item (swap).
5. Keep buying until the board is full: a message says so and nothing is lost.
6. Open **▶ Debug** and tap **Grow board** to test expansion (up to 6x7). Stop and press Play again: the board,
   prices and size are restored from the save.
7. Run the tests: **Window > General > Test Runner > EditMode > Run All** (86 tests).

### Known issues
- Gold is unlimited until Phase 3 adds the economy (`UnlimitedWallet`).
- Text uses Unity's built-in font through legacy uGUI Text, not TextMeshPro yet (see DESIGN_NOTES.md).
- Not yet run inside Unity: the Unity-side code was compile-checked against Unity reference assemblies
  (UnityEngine 2021.1 and UnityEngine.UI 2020.3), not Unity 6.

## Next: Phase 3, Economy and Base
Currencies, gold mines, barracks, warehouse, HQ upgrades, offline earnings, EconomyConfig.
