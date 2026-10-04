using System;
using System.Collections;
using System.Collections.Generic;
using Game.App;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Events;
using Game.Core.Services;
using Game.Data;
using Game.Logic.Merge;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The Base scene's screen, built from code at startup: currency bar, merge board, buy buttons and the Battle button.
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

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly List<(string TypeId, Text Label)> _buyLabels = new List<(string, Text)>();

        private MergeBoardService _board;
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
            _visuals = services.Get<GameConfig>().MergeConfig;
            _audio = services.Get<IAudioService>();
            var bus = services.Get<EventBus>();
            Build(bus);

            _subscriptions.Add(bus.Subscribe<BoardChanged>(_ => RefreshHud()));
            _subscriptions.Add(bus.Subscribe<BuyFailed>(OnBuyFailed));
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
        }

        private void BuildTopBar(RectTransform parent)
        {
            var bar = Ui.Image("Top Bar", parent, UiStyle.Panel);
            Ui.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, TopBarHeight), Vector2.zero);
            bar.rectTransform.anchorMin = new Vector2(0, 1);
            bar.rectTransform.anchorMax = new Vector2(1, 1);

            var title = Ui.Label("Title", bar.transform, GameInfo.Title, UiStyle.TitleSize, UiStyle.Text, TextAnchor.MiddleLeft);
            Ui.Stretch(title.rectTransform, SideMargin, 0, 0, 0);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1);

            _goldText = Ui.Label("Gold", bar.transform, "", UiStyle.BodySize, UiStyle.Gold, TextAnchor.MiddleRight);
            Ui.Stretch(_goldText.rectTransform, 0, 0, SideMargin, 0);
            _goldText.rectTransform.anchorMin = new Vector2(0.5f, 0);
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
                Ui.Button("Buy " + typeId, grid, "", UiStyle.SmallSize, color, UiStyle.TextDark, () => _board.Buy(typeId), out var label);
                _buyLabels.Add((typeId, label));
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
            var wallet = _board.Wallet;
            _goldText.text = $"{Strings.Get("hud.gold")}: " +
                             (wallet is UnlimitedWallet ? Strings.Get("hud.unlimited") : NumberFormat.Short(wallet.Gold));

            foreach (var (typeId, label) in _buyLabels)
            {
                var entry = _visuals != null ? _visuals.Find(typeId) : null;
                var name = Strings.Get(entry != null ? entry.NameKey : typeId);
                label.text = $"{name} {Strings.Format("base.level", _board.BuyLevel(typeId))}\n{NumberFormat.Short(_board.BuyCost(typeId))}";
            }
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
