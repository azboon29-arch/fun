# Progress

| Phase | Status |
|---|---|
| 1. Foundation | Code done; waiting for first open in the Unity Editor |
| 2. Merge Board | Code done; waiting for an Editor play test |
| 3. Economy and Base | Code done; waiting for an Editor play test |
| 4. Battle Core | Code done; waiting for an Editor play test |
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
- 86 tests in total; 39 new for merge rules and edge cases (full board, max level, invalid drags, prices,
  buy level, save round trip, damaged save data, RNG determinism).

### How to try it in the Editor
1. Do Phase 1 steps 1 to 7 if you have not yet. If you already did, run **Tools > Merge Command > Build Scenes**
   again: it adds the MergeConfig asset and rebuilds the Base scene.
2. Press **Play**. The Base screen shows a 4x5 board and six buy buttons.
3. Tap **Infantry** twice, then drag one Infantry onto the other: they merge into L2 with a bounce.
4. Drag an item to an empty slot (move) and onto a different item (swap).
5. Keep buying until the board is full: a message says so and nothing is lost.
6. Stop and press Play again: the board, prices and size are restored from the save. (Since Phase 3 the board grows
   through HQ upgrades instead of the debug button.)
7. Run the tests: **Window > General > Test Runner > EditMode > Run All** (86 tests).

### Known issues
- Text uses Unity's built-in font through legacy uGUI Text, not TextMeshPro yet (see DESIGN_NOTES.md).
- Not yet run inside Unity: the Unity-side code was compile-checked against Unity reference assemblies
  (UnityEngine 2021.1 and UnityEngine.UI 2020.3), not Unity 6.

## Phase 3: Economy and Base

### Done
- `CurrencyWallet`: Gold and Gems that never go negative and never overflow, saved under `wallet`. It now pays for board purchases.
- `EconomyService`:
  - Gold Mines earn `base × 2.1^(level−1)` gold per second while you play, with fractional gold carried between frames.
  - A gap longer than 60 s (app closed or in the background) is paid as offline earnings. The default cap is 8 h,
    each Warehouse level adds 0.5 h, and the cap stops at 24 h. These earnings are reported for the "Welcome back" popup.
  - Barracks give 2% off units per level, capped at 50%.
- `HqService`: HQ levels 1 to 10 set the board size (4x5 up to 6x7) and the highest buy level. They are re-applied after every load.
- `EconomyConfig` asset (`Assets/_Game/Data/EconomyConfig.asset`, created by Build Scenes) holds every number above,
  plus the starting balances (500 gold) and the HQ level table.
- Base screen: gold, income per second and gems in the top bar, an HQ button that opens the upgrade popup,
  a "Welcome back" popup after time away, and buy buttons dimmed when you can't afford them.
- Debug overlay: **+10,000 gold**, and **Away 2h**, which jumps the game clock forward to trigger offline earnings.
- 114 tests in total. The 28 new ones cover the wallet, income while playing and away (with a mocked clock), caps,
  Warehouses, Barracks, HQ upgrades and limits, and save/reload of all of it.

### How to try it in the Editor
1. Run **Tools > Merge Command > Build Scenes** again. It adds the EconomyConfig asset and keeps your other assets.
2. Press **Play**. You start with 500 gold. Buy a **Gold Mine** and watch the gold climb.
3. Buy a second Gold Mine and merge the two. The income shown in the top bar goes up.
4. Tap **HQ 1**, then use **▶ Debug > +10,000 gold** and **Upgrade**. The board grows to 5x5.
5. Tap **Away 2h** in the debug panel. The "Welcome back" popup shows what your mines earned.
6. Stop Play, wait a few minutes, and press Play again. Offline earnings are paid on return.
7. Buy a Barracks. Unit prices on the buy buttons drop by 2%.
8. Run the tests: **Window > General > Test Runner > EditMode > Run All** (114 tests).

## Phase 4: Battle Core

### Done
- `BattleSimulation` (plain C#, integer math only): fixed 20 steps per second, one seeded RNG, the same setup always
  gives the same battle. Units move toward their target until in range, then attack on a cooldown.
- Targeting: only enemies in the frontmost row that still has a living unit can be targeted, then the nearest, then
  the lowest HP. So the front row takes the hits and the back row is safe until it falls.
- Counters: Infantry beats Artillery, Artillery beats Armor, Armor beats Infantry, +50% damage (configurable).
  Damage has a small seeded ±5% spread.
- Stats grow ×1.9 per merge level (HP and attack), so power grows about ×1.9 per merge.
- Result: victory, or defeat by wipe-out or time limit. Stars: win, win with no losses, win within the level's fast-win time.
- `Formation`: a 3x5 grid (front, middle, back) with the level's deployment limit. A board unit can be placed once;
  a remembered formation drops units that were merged or moved away on the board.
- `BattleService`: roster of board units (strongest first), Auto fill, tap to add, swap slots, rewards
  (gold per win, gems for each new star), best stars per level saved under `campaign`, next level unlocks on a win.
  Units are never lost in battle.
- `BattleConfig` asset (rules and the three unit types) and `CampaignConfig` asset (three test levels), both created
  by Build Scenes.
- Battle scene built from code:
  - Planning: the scouted enemy formation, your formation grid, the power comparison marked as a rough guide,
    a roster you drag from (or tap), Auto, Clear and Fight.
  - Battle: units with HP bars, smoothed movement, damage numbers (gold when a counter applied), 1x/2x speed, Retreat.
  - Result: Victory or Defeat, the three star objectives, rewards, a tip after a defeat saying which class beat
    yours, and Retry (back to planning with the same formation), Next level and Base.
- 166 tests in total. The 52 new ones include running the same battle 100 times with identical results, front-row
  protection, targeting order, counters, range and movement, time limit, stars, formation rules, rewards and saving.

### How to try it in the Editor
1. Run **Tools > Merge Command > Build Scenes** again. It adds the BattleConfig and CampaignConfig assets and builds
   the Battle scene.
2. Press **Play**. Buy three Infantry (or use **▶ Debug > +10,000 gold** first), then tap **Battle**.
3. The planning screen shows level 1-1 with two enemy Infantry at the top and your units already placed by Auto.
   Drag a unit from the roster onto a slot, drag placed units between slots, tap one to remove it.
4. Tap **Fight!**. Try the **1x/2x** button. The result panel shows stars and the gold reward.
5. Tap **Retry**: your formation is still there. Tapping Fight again with no change replays the exact same battle.
6. Win 1-1 to unlock 1-2 (use **>** in the top bar or **Next level**). Lose on purpose with one Infantry against 1-3
   to see the defeat tip.
7. Stop and press Play again: best stars and unlocked levels are restored.
8. Run the tests: **Window > General > Test Runner > EditMode > Run All** (166 tests).

### Known issues
- Not yet run inside Unity (same as Phases 1 to 3).
- The commander ability pick, traits and the full campaign map come in Phases 5 and 6.
- Units are drawn as colored circles with labels; art and sound come in Phase 7.

## Next: Phase 5, Strategy Layer
Commander abilities, unit traits, and the systems that make planning choices matter.
