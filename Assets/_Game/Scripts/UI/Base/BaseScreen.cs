using System;
using System.Collections;
using System.Collections.Generic;
using Game.App;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Events;
using Game.Core.Services;
using Game.Data;
using Game.Logic.Base;
using Game.Logic.Economy;
using Game.Logic.Merge;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The Base scene's screen, built from code at startup: currency bar with the HQ button, merge board, buy buttons,
    /// the Battle button, and the HQ and Welcome back popups.
    /// </summary>
    public sealed class BaseScreen : MonoBehaviour
    {
        private const float TopBarHeight = 150f;
        private const float BottomHeight = 520f;
        private const float SideMargin = 32f;
        private const float BuyButtonHeight = 150f;
        private const float BattleButtonHeight = 140f;
        private const float ButtonGap = 16f;
        private const int BuyColumns = 3;
        private const float ToastHeight = 110f;
        private const float ToastSeconds = 1.6f;
        private const float ToastFadeSeconds = 0.4f;
        private const float HqButtonWidth = 260f;
        private const float HudRefreshSeconds = 0.25f;
        private const float UnaffordableAlpha = 0.45f;
        private const int SecondsPerMinute = 60;
        private const int MinutesPerHour = 60;

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly List<(string TypeId, Text Label, CanvasGroup Group)> _buyButtons = new List<(string, Text, CanvasGroup)>();

        private MergeBoardService _board;
        private CurrencyWallet _wallet;
        private EconomyService _economy;
        private HqService _hq;
        private Transform _popupRoot;
        private Popup _hqPopup;
        private Text _hqButtonText;
        private float _nextHudRefresh;
        private MergeConfigAsset _visuals;
        private IAudioService _audio;
        private Text _goldText;
        private Text _toastText;
        private CanvasGroup _toast;
        private Coroutine _toastRoutine;

        private void Start()
        {
            var services = GameServices.Current;
            if (services == null) return; // BootGuard is loading the Boot scene; it brings us back here.

            _board = services.Get<MergeBoardService>();
            _wallet = services.Get<CurrencyWallet>();
            _economy = services.Get<EconomyService>();
            _hq = services.Get<HqService>();
            _visuals = services.Get<GameConfig>().MergeConfig;
            _audio = services.Get<IAudioService>();
            var bus = services.Get<EventBus>();
            Build(bus);

            _subscriptions.Add(bus.Subscribe<BoardChanged>(_ => RefreshHud()));
            _subscriptions.Add(bus.Subscribe<BuyFailed>(OnBuyFailed));
            _subscriptions.Add(bus.Subscribe<CurrencyChanged>(_ => RefreshHud()));
            _subscriptions.Add(bus.Subscribe<HqUpgraded>(e => ShowToast(Strings.Format("hq.upgraded", e.Level))));
            _subscriptions.Add(bus.Subscribe<OfflineEarnings>(_ => ShowOfflineReport()));
            RefreshHud();
            ShowOfflineReport(); // earnings computed during boot, before this screen existed
        }

        private void Update()
        {
            // Income changes the balance every frame; redraw the HUD a few times a second, not every frame.
            if (_board == null || Time.unscaledTime < _nextHudRefresh) return;
            RefreshHud();
        }

        private void OnDestroy()
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
        }

        private void Build(EventBus bus)
        {
            var canvas = Ui.CreateCanvas("Base Canvas");
            canvas.transform.SetParent(transform, false);
            _popupRoot = canvas.transform;
            var background = Ui.Image("Background", canvas.transform, UiStyle.Background);
            Ui.Stretch(background.rectTransform);

            var safe = Ui.Rect("Safe Area", canvas.transform);
            Ui.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildTopBar(safe);

            var boardArea = Ui.Rect("Board", safe);
            Ui.Stretch(boardArea, SideMargin, BottomHeight, SideMargin, TopBarHeight + SideMargin);
            var dragLayer = Ui.Rect("Drag Layer", canvas.transform);
            Ui.Stretch(dragLayer);
            var boardView = boardArea.gameObject.AddComponent<BoardView>();
            boardView.Init(_board, _visuals, bus, _audio, dragLayer);
            boardView.MaxLevelDrop += () => ShowToast(Strings.Get("base.maxLevel"));

            BuildBottom(safe);
            BuildToast(safe);
            dragLayer.SetAsLastSibling();
        }

        private void BuildTopBar(RectTransform parent)
        {
            var bar = Ui.Image("Top Bar", parent, UiStyle.Panel);
            Ui.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, TopBarHeight), Vector2.zero);
            bar.rectTransform.anchorMin = new Vector2(0, 1);
            bar.rectTransform.anchorMax = new Vector2(1, 1);

            _goldText = Ui.Label("Currencies", bar.transform, "", UiStyle.BodySize, UiStyle.Gold, TextAnchor.MiddleLeft);
            Ui.Stretch(_goldText.rectTransform, SideMargin, 0, HqButtonWidth + SideMargin * 2, 0);

            var hqButton = Ui.Button("HQ", bar.transform, "", UiStyle.BodySize, UiStyle.Secondary, UiStyle.Text, OpenHq, out _hqButtonText);
            var rect = (RectTransform)hqButton.transform;
            rect.anchorMin = new Vector2(1, 0.5f);
            rect.anchorMax = new Vector2(1, 0.5f);
            rect.pivot = new Vector2(1, 0.5f);
            rect.sizeDelta = new Vector2(HqButtonWidth, Mathf.Min(UiStyle.MinTouch, TopBarHeight - 10f));
            rect.anchoredPosition = new Vector2(-SideMargin, 0);
        }

        private void BuildBottom(RectTransform parent)
        {
            var bottom = Ui.Image("Bottom Panel", parent, UiStyle.Panel);
            bottom.rectTransform.anchorMin = Vector2.zero;
            bottom.rectTransform.anchorMax = new Vector2(1, 0);
            bottom.rectTransform.pivot = new Vector2(0.5f, 0);
            bottom.rectTransform.sizeDelta = new Vector2(0, BottomHeight - SideMargin);
            bottom.rectTransform.anchoredPosition = Vector2.zero;

            var grid = Ui.Rect("Buy Buttons", bottom.transform);
            Ui.Stretch(grid, SideMargin, BattleButtonHeight + SideMargin * 2, SideMargin, SideMargin);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = BuyColumns;
            layout.spacing = new Vector2(ButtonGap, ButtonGap);
            layout.childAlignment = TextAnchor.UpperCenter;
            var buttonWidth = (Ui.ReferenceResolution.x - SideMargin * 2 - ButtonGap * (BuyColumns - 1)) / BuyColumns;
            layout.cellSize = new Vector2(buttonWidth, Mathf.Max(UiStyle.MinTouch, BuyButtonHeight));

            foreach (var item in _board.Config.Items)
            {
                var typeId = item.Id;
                var entry = _visuals != null ? _visuals.Find(typeId) : null;
                var color = entry?.Color ?? UiStyle.Secondary;
                var button = Ui.Button("Buy " + typeId, grid, "", UiStyle.SmallSize, color, UiStyle.TextDark, () => _board.Buy(typeId), out var label);
                _buyButtons.Add((typeId, label, button.gameObject.AddComponent<CanvasGroup>()));
            }

            var battle = Ui.Button("Battle", bottom.transform, Strings.Get("base.battle"), UiStyle.TitleSize, UiStyle.Primary,
                UiStyle.TextDark, () => SceneManager.LoadScene(SceneNames.Battle), out _);
            var rect = (RectTransform)battle.transform;
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(-SideMargin * 2, Mathf.Max(UiStyle.MinTouch, BattleButtonHeight));
            rect.anchoredPosition = new Vector2(0, SideMargin);
        }

        private void BuildToast(RectTransform parent)
        {
            var toast = Ui.Image("Toast", parent, UiStyle.Panel, PlaceholderSprites.RoundedSquare);
            toast.raycastTarget = false;
            var rect = toast.rectTransform;
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(-SideMargin * 2, ToastHeight);
            rect.anchoredPosition = new Vector2(0, BottomHeight);
            _toast = toast.gameObject.AddComponent<CanvasGroup>();
            _toast.alpha = 0f;
            _toast.blocksRaycasts = false;
            _toastText = Ui.Label("Text", toast.transform, "", UiStyle.BodySize, UiStyle.Text);
            Ui.Stretch(_toastText.rectTransform, SideMargin, 0, SideMargin, 0);
        }

        private void RefreshHud()
        {
            _nextHudRefresh = Time.unscaledTime + HudRefreshSeconds;
            _goldText.text =
                $"{Strings.Get("hud.gold")} {NumberFormat.Short(_wallet.Gold)}  " +
                $"{Strings.Format("hud.perSecond", NumberFormat.Short((long)Math.Floor(_economy.IncomePerSecond)))}\n" +
                $"{Strings.Get("hud.gems")} {NumberFormat.Short(_wallet.Gems)}";
            _hqButtonText.text = Strings.Format("hud.hq", _hq.Level);

            foreach (var (typeId, label, group) in _buyButtons)
            {
                var entry = _visuals != null ? _visuals.Find(typeId) : null;
                var name = Strings.Get(entry != null ? entry.NameKey : typeId);
                var cost = _board.BuyCost(typeId);
                label.text = $"{name} {Strings.Format("base.level", _board.BuyLevel(typeId))}\n{NumberFormat.Short(cost)}";
                // Still tappable when too expensive, so the player gets the "not enough gold" message.
                group.alpha = _wallet.Gold >= cost ? 1f : UnaffordableAlpha;
            }
            RefreshHqPopup();
        }

        private void OpenHq()
        {
            if (_hqPopup != null) return;
            _hqPopup = Popup.Create(_popupRoot, "", "");
            _hqPopup.Closed += () => _hqPopup = null;
            if (!_hq.IsMaxLevel)
            {
                _hqPopup.AddButton(Strings.Format("hq.upgrade", NumberFormat.Short(_hq.Next.UpgradeCost)), UiStyle.Primary, UpgradeHq, closes: false);
            }
            _hqPopup.AddButton(Strings.Get("common.close"), UiStyle.Secondary, null);
            RefreshHqPopup();
        }

        private void UpgradeHq()
        {
            var failure = _hq.TryUpgrade();
            if (failure == HqUpgradeFailure.NotEnoughGold)
            {
                _audio.PlaySfx(SfxIds.Denied);
                ShowToast(Strings.Get("hq.notEnoughGold"));
                return;
            }
            _audio.PlaySfx(SfxIds.Buy);
            // Rebuild so the upgrade button shows the next price (or disappears at max level).
            _hqPopup?.Close();
            OpenHq();
        }

        private void RefreshHqPopup()
        {
            if (_hqPopup == null) return;
            var current = _hq.Current;
            var body = Strings.Format("hq.current", current.BoardColumns, current.BoardRows, current.MaxBuyLevel) + "\n\n" +
                       (_hq.IsMaxLevel
                           ? Strings.Get("hq.max")
                           : Strings.Format("hq.next", _hq.Next.BoardColumns, _hq.Next.BoardRows, _hq.Next.MaxBuyLevel));
            _hqPopup.SetText($"{Strings.Get("hq.title")} · {Strings.Format("hq.level", _hq.Level)}", body);
        }

        private void ShowOfflineReport()
        {
            var report = _economy.TakeOfflineReport();
            if (!report.HasValue) return;
            var r = report.Value;
            var body = Strings.Format("offline.body", NumberFormat.Short(r.Gold), FormatDuration(r.SecondsAway));
            if (r.WasCapped) body += "\n\n" + Strings.Format("offline.capped", FormatDuration(r.SecondsPaid));
            var popup = Popup.Create(_popupRoot, Strings.Get("offline.title"), body);
            popup.AddButton(Strings.Get("offline.collect"), UiStyle.Primary, () => _audio.PlaySfx(SfxIds.Buy));
        }

        private static string FormatDuration(double seconds)
        {
            var totalMinutes = (long)Math.Floor(seconds / SecondsPerMinute);
            var hours = totalMinutes / MinutesPerHour;
            var minutes = totalMinutes % MinutesPerHour;
            return hours > 0 ? Strings.Format("time.hoursMinutes", hours, minutes) : Strings.Format("time.minutes", minutes);
        }

        private void OnBuyFailed(BuyFailed e)
        {
            _audio.PlaySfx(SfxIds.Denied);
            switch (e.Reason)
            {
                case BuyFailure.BoardFull:
                    ShowToast(Strings.Get("base.boardFull"));
                    break;
                case BuyFailure.NotEnoughGold:
                    ShowToast(Strings.Get("base.notEnoughGold"));
                    break;
            }
        }

        private void ShowToast(string message)
        {
            _toastText.text = message;
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ShowToastRoutine());
        }

        private IEnumerator ShowToastRoutine()
        {
            yield return Tween.ShowThenFade(_toast, ToastSeconds, ToastFadeSeconds);
            _toastRoutine = null;
        }
    }
}
