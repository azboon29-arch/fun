using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.App;
using Game.Core.Audio;
using Game.Core.Services;
using Game.Data;
using Game.Logic.Battle;
using Game.Logic.Campaign;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The Battle scene's screen, built from code: the planning view (scouted enemy, formation grid, roster of board
    /// units, power comparison), the live battle with a 1x/2x speed toggle, and the result panel with stars, rewards
    /// and Retry (which keeps the formation).
    /// </summary>
    public sealed class BattleScreen : MonoBehaviour
    {
        private const float TopBarHeight = 150f;
        private const float BottomBarHeight = 190f;
        private const float SideMargin = 32f;
        private const float Gap = 16f;
        private const float CaptionHeight = 64f;
        private const float PowerHeight = 96f;
        private const float RosterHeight = 210f;
        private const float RosterChipWidth = 190f;
        private const float BackButtonWidth = 220f;
        private const float ArrowWidth = 120f;
        private const float SpeedButtonWidth = 180f;
        private const float ResultPanelWidth = 940f;
        private const float ResultPanelHeight = 1240f;
        private const float ObjectiveRowHeight = 110f;
        private const float StarSize = 90f;
        private const float ResultDelaySeconds = 0.8f;
        private const float PlacedAlpha = 0.35f;
        private const float PopInSeconds = 0.2f;
        private const float StarPopSeconds = 0.35f;
        private const int ResultTitleSize = UiStyle.TitleSize + 16;
        private const int MaxStepsPerFrame = 12;
        private const int SecondsPerMinute = 60;
        private static readonly int[] Speeds = { 1, 2 };
        private static readonly Color StarOff = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color PanelColor = new Color(0.16f, 0.2f, 0.16f, 1f);

        private BattleService _battles;
        private MergeConfigAsset _visuals;
        private IAudioService _audio;
        private string _levelId;

        private RectTransform _canvasRoot;
        private RectTransform _dragLayer;
        private Toast _toast;

        private RectTransform _planning;
        private Text _levelTitle;
        private Button _prevLevel;
        private Button _nextLevel;
        private FormationGridView _enemyGrid;
        private FormationGridView _playerGrid;
        private Text _yoursCaption;
        private Text _powerText;
        private ScrollRect _rosterScroll;
        private RectTransform _rosterContent;
        private Text _rosterEmpty;

        private RectTransform _fight;
        private BattleFieldView _field;
        private Text _timer;
        private Text _speedLabel;
        private BattleSimulation _sim;
        private float _accumulator;
        private int _speedIndex;
        private bool _resultShown;

        private GameObject _result;
        private UnitBadge _ghost;

        private void Start()
        {
            var services = GameServices.Current;
            if (services == null) return; // BootGuard is loading the Boot scene; it brings us back here.

            _battles = services.Get<BattleService>();
            _visuals = services.Get<GameConfig>().MergeConfig;
            _audio = services.Get<IAudioService>();
            _levelId = _battles.Progress.CurrentLevel?.Id;
            Build();
            OpenPlanning();
        }

        private void Build()
        {
            var canvas = Ui.CreateCanvas("Battle Canvas");
            canvas.transform.SetParent(transform, false);
            _canvasRoot = (RectTransform)canvas.transform;
            var background = Ui.Image("Background", canvas.transform, UiStyle.Background);
            Ui.Stretch(background.rectTransform);

            var safe = Ui.Rect("Safe Area", canvas.transform);
            Ui.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            _planning = Ui.Rect("Planning", safe);
            Ui.Stretch(_planning);
            BuildPlanning(_planning);

            _fight = Ui.Rect("Fight", safe);
            Ui.Stretch(_fight);
            BuildFight(_fight);

            _dragLayer = Ui.Rect("Drag Layer", canvas.transform);
            Ui.Stretch(_dragLayer);
            _toast = Toast.Create(safe, BottomBarHeight + Gap);
        }

        // ---------------------------------------------------------------- planning

        private void BuildPlanning(RectTransform root)
        {
            var bar = TopBar(root);
            var back = Ui.Button("Back", bar, Strings.Get("battle.back"), UiStyle.BodySize, UiStyle.Secondary, UiStyle.Text, GoToBase, out _);
            Anchor((RectTransform)back.transform, new Vector2(0, 0.5f), new Vector2(BackButtonWidth, UiStyle.MinTouch), new Vector2(SideMargin, 0));
            _prevLevel = Ui.Button("Previous Level", bar, "<", UiStyle.TitleSize, UiStyle.Secondary, UiStyle.Text, () => ChangeLevel(-1), out _);
            Anchor((RectTransform)_prevLevel.transform, new Vector2(0, 0.5f), new Vector2(ArrowWidth, UiStyle.MinTouch), new Vector2(SideMargin * 2 + BackButtonWidth, 0));
            _nextLevel = Ui.Button("Next Level", bar, ">", UiStyle.TitleSize, UiStyle.Secondary, UiStyle.Text, () => ChangeLevel(1), out _);
            Anchor((RectTransform)_nextLevel.transform, new Vector2(1, 0.5f), new Vector2(ArrowWidth, UiStyle.MinTouch), new Vector2(-SideMargin, 0));
            _levelTitle = Ui.Label("Title", bar, "", UiStyle.BodySize, UiStyle.Gold);
            Ui.Stretch(_levelTitle.rectTransform, SideMargin * 3 + BackButtonWidth + ArrowWidth, 0, SideMargin * 2 + ArrowWidth, 0);

            var middle = Ui.Rect("Middle", root);
            Ui.Stretch(middle, SideMargin, BottomBarHeight, SideMargin, TopBarHeight + Gap);
            var layout = middle.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = Gap;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            Caption(middle, Strings.Get("battle.enemy"));
            _enemyGrid = Grid(middle, "Enemy Grid", true);
            _powerText = Ui.Label("Power", middle, "", UiStyle.SmallSize, UiStyle.Text);
            Fixed(_powerText.gameObject, PowerHeight);
            _playerGrid = Grid(middle, "Player Grid", false);
            _yoursCaption = Caption(middle, "");
            Caption(middle, Strings.Get("battle.roster"));
            BuildRoster(middle);

            _playerGrid.UnitDragStarted = (slot, chip, e) => BeginGhostDrag(_battles.FormationFor(_levelId).Get(slot.Row, slot.Column), e, () => _playerGrid.SetDimmed(slot, true));
            _playerGrid.UnitDragging = (chip, e) => ContinueGhostDrag(e);
            _playerGrid.UnitDragEnded = (slot, chip, e) => EndSlotDrag(slot, e);
            _playerGrid.UnitTapped = slot =>
            {
                _battles.FormationFor(_levelId).Remove(slot.Row, slot.Column);
                _audio.PlaySfx(SfxIds.Drop);
                RefreshPlanning();
            };

            var bottom = BottomBar(root);
            var buttons = Ui.Rect("Buttons", bottom);
            Ui.Stretch(buttons, SideMargin, Gap, SideMargin, Gap);
            var row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = Gap;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            Ui.Button("Auto", buttons, Strings.Get("battle.auto"), UiStyle.BodySize, UiStyle.Secondary, UiStyle.Text, () =>
            {
                _battles.AutoFill(_levelId);
                RefreshPlanning();
            }, out _);
            Ui.Button("Clear", buttons, Strings.Get("battle.clear"), UiStyle.BodySize, UiStyle.Secondary, UiStyle.Text, () =>
            {
                _battles.FormationFor(_levelId).Clear();
                RefreshPlanning();
            }, out _);
            var fight = Ui.Button("Fight", buttons, Strings.Get("battle.fight"), UiStyle.TitleSize, UiStyle.Primary, UiStyle.TextDark, StartBattle, out _);
            fight.gameObject.AddComponent<LayoutElement>().flexibleWidth = 2f;
        }

        private void BuildRoster(RectTransform parent)
        {
            var viewport = Ui.Image("Roster", parent, UiStyle.Panel, PlaceholderSprites.RoundedSquare);
            Fixed(viewport.gameObject, RosterHeight);
            viewport.gameObject.AddComponent<RectMask2D>();
            _rosterScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _rosterScroll.horizontal = true;
            _rosterScroll.vertical = false;
            _rosterScroll.movementType = ScrollRect.MovementType.Clamped;
            _rosterScroll.viewport = viewport.rectTransform;

            _rosterContent = Ui.Rect("Content", viewport.transform);
            _rosterContent.anchorMin = new Vector2(0, 0);
            _rosterContent.anchorMax = new Vector2(0, 1);
            _rosterContent.pivot = new Vector2(0, 0.5f);
            _rosterContent.offsetMin = _rosterContent.offsetMax = Vector2.zero;
            var row = _rosterContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = Gap;
            row.padding = new RectOffset((int)Gap, (int)Gap, (int)Gap, (int)Gap);
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;
            _rosterContent.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            _rosterScroll.content = _rosterContent;

            _rosterEmpty = Ui.Label("Empty", viewport.transform, Strings.Get("battle.rosterEmpty"), UiStyle.SmallSize, UiStyle.Text);
            Ui.Stretch(_rosterEmpty.rectTransform, SideMargin, 0, SideMargin, 0);
        }

        private void OpenPlanning()
        {
            CloseResult();
            _sim = null;
            _fight.gameObject.SetActive(false);
            _planning.gameObject.SetActive(true);
            if (_levelId != null && _battles.FormationFor(_levelId).Count == 0) _battles.AutoFill(_levelId);
            RefreshPlanning();
        }

        private void ChangeLevel(int delta)
        {
            var levels = _battles.Campaign.Levels;
            var index = _battles.Campaign.IndexOf(_levelId) + delta;
            if (index < 0 || index >= levels.Count || !_battles.Progress.IsUnlocked(levels[index].Id)) return;
            _levelId = levels[index].Id;
            OpenPlanning();
        }

        private void RefreshPlanning()
        {
            var level = _battles.Campaign.Find(_levelId);
            if (level == null)
            {
                _levelTitle.text = "";
                return;
            }
            var index = _battles.Campaign.IndexOf(_levelId);
            var stars = _battles.Progress.Stars(_levelId);
            _levelTitle.text = Strings.Format("battle.level", level.Id, level.Name) + (stars > 0 ? $"  ({stars}/{BattleResult.MaxStars})" : "");
            _prevLevel.interactable = index > 0;
            _nextLevel.interactable = index + 1 < _battles.Campaign.Levels.Count && _battles.Progress.IsUnlocked(_battles.Campaign.Levels[index + 1].Id);

            var formation = _battles.FormationFor(_levelId);
            var enemy = level.CreateEnemyFormation();
            _enemyGrid.Show(enemy);
            _playerGrid.Show(formation);
            _yoursCaption.text = Strings.Format("battle.yours", formation.Count, level.DeploymentLimit);
            _powerText.text = Strings.Format("battle.power", NumberFormat.Short(formation.Power(_battles.Config)), NumberFormat.Short(enemy.Power(_battles.Config)))
                              + "\n" + Strings.Get("battle.counters");
            RefreshRoster(formation);
        }

        private void RefreshRoster(Formation formation)
        {
            Ui.ClearChildren(_rosterContent);
            var roster = _battles.Roster();
            _rosterEmpty.gameObject.SetActive(roster.Count == 0);
            foreach (var unit in roster)
            {
                var chipRoot = Ui.Rect($"Chip {unit.Cell}", _rosterContent);
                var element = chipRoot.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = RosterChipWidth;
                var hit = chipRoot.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                var holder = Ui.Rect("Holder", chipRoot);
                Ui.Stretch(holder, Gap, Gap, Gap, Gap);
                UnitBadge.Create("Badge", holder, false).Show(unit.Item.TypeId, unit.Item.Level, _visuals, false);
                var placed = formation.Uses(unit.Cell);
                chipRoot.gameObject.AddComponent<CanvasGroup>().alpha = placed ? PlacedAlpha : 1f;

                var chip = chipRoot.gameObject.AddComponent<DragChip>();
                chip.ForwardSidewaysDragsTo(_rosterScroll);
                var captured = unit;
                chip.Tapped = _ => ToggleRosterUnit(captured);
                chip.DragStarted = (c, e) => BeginGhostDrag(new FormationEntry(FormationRow.Front, 0, captured.Item.TypeId, captured.Item.Level, captured.Cell), e, null);
                chip.Dragging = (c, e) => ContinueGhostDrag(e);
                chip.DragEnded = (c, e) => EndRosterDrag(captured, e);
            }
        }

        private void ToggleRosterUnit(RosterUnit unit)
        {
            var formation = _battles.FormationFor(_levelId);
            if (formation.Uses(unit.Cell))
            {
                var entry = formation.Entries.First(e => e.SourceCell == unit.Cell);
                formation.Remove(entry.Row, entry.Column);
                _audio.PlaySfx(SfxIds.Drop);
            }
            else if (_battles.PlaceInFreeSlot(_levelId, unit))
            {
                _audio.PlaySfx(SfxIds.Drop);
            }
            else
            {
                DenyLimit();
            }
            RefreshPlanning();
        }

        private void BeginGhostDrag(FormationEntry? entry, PointerEventData e, System.Action onStarted)
        {
            if (!entry.HasValue) return;
            DestroyGhost();
            var holder = Ui.Rect("Ghost", _dragLayer);
            holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = Vector2.one * Mathf.Max(UiStyle.MinTouch, _playerGrid.CellSize);
            _ghost = UnitBadge.Create("Badge", holder, false);
            _ghost.Show(entry.Value.UnitId, entry.Value.Level, _visuals, false);
            holder.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            onStarted?.Invoke();
            ContinueGhostDrag(e);
        }

        private void ContinueGhostDrag(PointerEventData e)
        {
            if (_ghost == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragLayer, e.position, e.pressEventCamera, out var local))
            {
                ((RectTransform)_ghost.transform.parent).localPosition = local;
            }
            _playerGrid.Highlight(_playerGrid.SlotAt(e));
        }

        private void EndRosterDrag(RosterUnit unit, PointerEventData e)
        {
            var target = _playerGrid.SlotAt(e);
            DestroyGhost();
            if (target.HasValue)
            {
                var level = _battles.Campaign.Find(_levelId);
                var entry = new FormationEntry(target.Value.Row, target.Value.Column, unit.Item.TypeId, unit.Item.Level, unit.Cell);
                if (_battles.FormationFor(_levelId).Place(entry, level.DeploymentLimit)) _audio.PlaySfx(SfxIds.Drop);
                else DenyLimit();
            }
            RefreshPlanning();
        }

        private void EndSlotDrag((FormationRow Row, int Column) from, PointerEventData e)
        {
            var target = _playerGrid.SlotAt(e);
            DestroyGhost();
            // Dropping outside the grid takes the unit out of the formation.
            if (target.HasValue) _battles.MoveOrSwap(_levelId, from, target.Value);
            else _battles.FormationFor(_levelId).Remove(from.Row, from.Column);
            _audio.PlaySfx(SfxIds.Drop);
            RefreshPlanning();
        }

        private void DestroyGhost()
        {
            _playerGrid.Highlight(null);
            if (_ghost != null) Destroy(_ghost.transform.parent.gameObject);
            _ghost = null;
        }

        private void DenyLimit()
        {
            _audio.PlaySfx(SfxIds.Denied);
            var level = _battles.Campaign.Find(_levelId);
            _toast.Show(Strings.Format("battle.limit", level?.DeploymentLimit ?? 0));
        }

        // ---------------------------------------------------------------- battle

        private void BuildFight(RectTransform root)
        {
            var bar = TopBar(root);
            var retreat = Ui.Button("Retreat", bar, Strings.Get("battle.retreat"), UiStyle.BodySize, UiStyle.Secondary, UiStyle.Text, OpenPlanning, out _);
            Anchor((RectTransform)retreat.transform, new Vector2(0, 0.5f), new Vector2(BackButtonWidth, UiStyle.MinTouch), new Vector2(SideMargin, 0));
            var speed = Ui.Button("Speed", bar, "", UiStyle.BodySize, UiStyle.Primary, UiStyle.TextDark, ToggleSpeed, out _speedLabel);
            Anchor((RectTransform)speed.transform, new Vector2(1, 0.5f), new Vector2(SpeedButtonWidth, UiStyle.MinTouch), new Vector2(-SideMargin, 0));
            _timer = Ui.Label("Timer", bar, "", UiStyle.TitleSize, UiStyle.Gold);
            Ui.Stretch(_timer.rectTransform, BackButtonWidth + SideMargin * 2, 0, SpeedButtonWidth + SideMargin * 2, 0);

            var field = Ui.Rect("Field", root);
            Ui.Stretch(field, SideMargin, SideMargin, SideMargin, TopBarHeight + Gap);
            _field = field.gameObject.AddComponent<BattleFieldView>();
        }

        private void StartBattle()
        {
            switch (_battles.CanStart(_levelId))
            {
                case BattleStartFailure.EmptyFormation:
                    _audio.PlaySfx(SfxIds.Denied);
                    _toast.Show(Strings.Get("battle.emptyFormation"));
                    return;
                case BattleStartFailure.Locked:
                case BattleStartFailure.UnknownLevel:
                    _audio.PlaySfx(SfxIds.Denied);
                    _toast.Show(Strings.Get("battle.locked"));
                    return;
            }

            _sim = _battles.Start(_levelId);
            _accumulator = 0f;
            _resultShown = false;
            _planning.gameObject.SetActive(false);
            _fight.gameObject.SetActive(true);
            _field.Show(_sim, _battles.Config, _visuals);
            RefreshSpeed();
            RefreshTimer();
        }

        private void Update()
        {
            if (_sim == null || _resultShown) return;
            if (!_sim.IsOver)
            {
                // Fixed timestep: the simulation always advances in whole ticks; speed only changes how many per frame.
                var step = 1f / _sim.TickRate;
                _accumulator += Time.deltaTime * Speeds[_speedIndex];
                var steps = 0;
                while (_accumulator >= step && !_sim.IsOver && steps < MaxStepsPerFrame)
                {
                    _sim.Step();
                    _field.OnTick(_sim.LastTickEvents);
                    _accumulator -= step;
                    steps++;
                }
                if (steps == MaxStepsPerFrame) _accumulator = 0f; // a long hitch slows the battle down instead of jumping
                RefreshTimer();
            }
            if (_sim.IsOver)
            {
                _resultShown = true;
                var result = _sim.BuildResult();
                var reward = _battles.Complete(_levelId, result);
                StartCoroutine(ShowResultAfterDelay(result, reward));
            }
        }

        private void ToggleSpeed()
        {
            _speedIndex = (_speedIndex + 1) % Speeds.Length;
            RefreshSpeed();
        }

        private void RefreshSpeed() => _speedLabel.text = Strings.Format("battle.speed", Speeds[_speedIndex]);

        private void RefreshTimer()
        {
            var seconds = Mathf.CeilToInt((float)_sim.RemainingSeconds);
            _timer.text = Strings.Format("battle.time", seconds / SecondsPerMinute, seconds % SecondsPerMinute);
        }

        // ---------------------------------------------------------------- result

        private IEnumerator ShowResultAfterDelay(BattleResult result, BattleReward reward)
        {
            yield return new WaitForSeconds(ResultDelaySeconds);
            ShowResult(result, reward);
        }

        private void ShowResult(BattleResult result, BattleReward reward)
        {
            CloseResult();
            var won = result.Outcome == BattleOutcome.Victory;
            _audio.PlaySfx(won ? SfxIds.Merge : SfxIds.Denied);
            var level = _battles.Campaign.Find(_levelId);

            var backdrop = Ui.Image("Result", _canvasRoot, Backdrop);
            Ui.Stretch(backdrop.rectTransform);
            _result = backdrop.gameObject;
            var panel = Ui.Image("Panel", backdrop.transform, PanelColor, PlaceholderSprites.RoundedSquare);
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(ResultPanelWidth, ResultPanelHeight), Vector2.zero);
            StartCoroutine(Tween.PopIn(panel.transform, PopInSeconds));

            var content = Ui.Rect("Content", panel.transform);
            Ui.Stretch(content, SideMargin * 1.5f, SideMargin * 1.5f, SideMargin * 1.5f, SideMargin * 1.5f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = Gap;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var title = Ui.Label("Title", content, Strings.Get(won ? "result.victory" : "result.defeat"), ResultTitleSize, UiStyle.Gold);
            Fixed(title.gameObject, CaptionHeight * 2);

            var objectives = new[]
            {
                Strings.Get("result.objWin"),
                Strings.Get("result.objNoLoss"),
                Strings.Format("result.objFast", level?.FastWinSeconds ?? 0),
            };
            for (var i = 0; i < objectives.Length; i++) ObjectiveRow(content, objectives[i], result.Objectives[i]);

            var lines = new List<string>();
            if (result.TimedOut) lines.Add(Strings.Get("result.timeout"));
            if (won)
            {
                lines.Add(reward.Gems > 0
                    ? Strings.Format("result.rewardGems", NumberFormat.Short(reward.Gold), NumberFormat.Short(reward.Gems))
                    : Strings.Format("result.reward", NumberFormat.Short(reward.Gold)));
            }
            else
            {
                lines.Add(Strings.Get("result.noReward"));
                var hint = LossHint(result);
                if (hint != null) lines.Add(hint);
            }
            var body = Ui.Label("Body", content, string.Join("\n\n", lines), UiStyle.BodySize, UiStyle.Text, TextAnchor.UpperCenter);
            body.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            var buttons = Ui.Rect("Buttons", content);
            Fixed(buttons.gameObject, UiStyle.MinTouch + Gap);
            var row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = Gap;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            Ui.Button("Base", buttons, Strings.Get("result.base"), UiStyle.BodySize, UiStyle.Secondary, UiStyle.Text, GoToBase, out _);
            // Retry returns to planning with the same formation already placed; Fight then replays it.
            Ui.Button("Retry", buttons, Strings.Get("result.retry"), UiStyle.BodySize, won ? UiStyle.Secondary : UiStyle.Primary,
                won ? UiStyle.Text : UiStyle.TextDark, OpenPlanning, out _);
            var next = NextUnlockedLevel();
            if (won && next != null)
            {
                Ui.Button("Next", buttons, Strings.Get("result.next"), UiStyle.BodySize, UiStyle.Primary, UiStyle.TextDark, () =>
                {
                    _levelId = next.Id;
                    OpenPlanning();
                }, out _);
            }
        }

        private void ObjectiveRow(RectTransform parent, string text, bool met)
        {
            var row = Ui.Rect("Objective", parent);
            Fixed(row.gameObject, ObjectiveRowHeight);
            var star = Ui.Image("Star", row, met ? UiStyle.Gold : StarOff, PlaceholderSprites.Star);
            star.raycastTarget = false;
            Anchor(star.rectTransform, new Vector2(0, 0.5f), new Vector2(StarSize, StarSize), new Vector2(SideMargin, 0));
            var label = Ui.Label("Text", row, text, UiStyle.BodySize, met ? UiStyle.Text : StarOff, TextAnchor.MiddleLeft);
            Ui.Stretch(label.rectTransform, SideMargin * 2 + StarSize, 0, 0, 0);
            if (met) StartCoroutine(Tween.PopIn(star.transform, StarPopSeconds));
        }

        private static string LossHint(BattleResult result)
        {
            if (result.PlayerDeathsBy.Count == 0) return null;
            var worst = result.PlayerDeathsBy.OrderByDescending(p => p.Value).ThenBy(p => (int)p.Key.Killer).First().Key;
            return Strings.Format("result.hint",
                Strings.Get("class." + worst.Victim),
                Strings.Get("class." + worst.Killer),
                Strings.Get("class." + Counters.CounteredBy(worst.Killer)));
        }

        private LevelDefinition NextUnlockedLevel()
        {
            var levels = _battles.Campaign.Levels;
            var index = _battles.Campaign.IndexOf(_levelId) + 1;
            return index > 0 && index < levels.Count && _battles.Progress.IsUnlocked(levels[index].Id) ? levels[index] : null;
        }

        private void CloseResult()
        {
            if (_result != null) Destroy(_result);
            _result = null;
        }

        private static void GoToBase() => SceneManager.LoadScene(SceneNames.Base);

        // ---------------------------------------------------------------- layout helpers

        private static RectTransform TopBar(RectTransform parent)
        {
            var bar = Ui.Image("Top Bar", parent, UiStyle.Panel);
            var rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(0, TopBarHeight);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private static RectTransform BottomBar(RectTransform parent)
        {
            var bar = Ui.Image("Bottom Bar", parent, UiStyle.Panel);
            var rect = bar.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(0, BottomBarHeight);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        private static Text Caption(RectTransform parent, string text)
        {
            var label = Ui.Label("Caption", parent, text, UiStyle.SmallSize, UiStyle.Text);
            Fixed(label.gameObject, CaptionHeight);
            return label;
        }

        private FormationGridView Grid(RectTransform parent, string name, bool enemy)
        {
            var rect = Ui.Rect(name, parent);
            rect.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var grid = rect.gameObject.AddComponent<FormationGridView>();
            grid.Init(enemy, _visuals);
            return grid;
        }

        private static void Fixed(GameObject go, float height)
        {
            var element = go.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;
        }
    }
}
