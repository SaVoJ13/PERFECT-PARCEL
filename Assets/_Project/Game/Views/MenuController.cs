using System.Collections.Generic;
using KebabStreet.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Головне меню (Q-05f): логотип-плейсхолдер з лампочками і парою, шапка (наставник, монети), шлях рівнів із жетонами,
    /// зірками й замками, виділення поточного рівня, картка рівня перед стартом, велика кнопка «Грати» і заглушка
    /// «Налаштування». Усе, що рухається, - transform/opacity (QB-ALL-16); час - <see cref="UiMotion.Now"/>.
    /// Правил гри тут немає: відкритість і зірки рахує <see cref="LevelProgress"/>.
    /// </summary>
    public sealed class MenuController : ScreenController
    {
        private const float RingInset = -10f;
        private const float RingWidth = 6f;
        private const float RingRadius = 85f;
        private const float LevelIconSize = 40f;
        private const int MaxStars = 3;
        private const float StarSize = 46f;
        private const float LockSize = 76f;
        private const float MarkerSize = 34f;
        private const float AliSize = 150f;
        private const float CoinIconSize = 52f;
        private const float BulbSize = 22f;
        private const float NodeBorder = 6f;
        private const float NodeFontSize = 72f;
        private const float CardFontSize = 38f;
        private const float CardOpenSec = 0.15f;
        private const float StarRowGap = 6f;
        private const int FxSeed = 7;
        private const float MaxFrameSec = 0.1f;
        private const float PlayWidthPercent = 70f;
        private const float SmallButtonHeight = 110f;

        private static readonly Color LockedFace = new Color(0.42f, 0.38f, 0.44f);
        private static readonly Color LockedText = new Color(0.78f, 0.74f, 0.8f);
        private static readonly Color DotOff = new Color(1f, 1f, 1f, 0.28f);
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.62f);
        private static readonly Color PanelFace = new Color(0.97f, 0.89f, 0.69f);

        private readonly List<MotionTarget> _cascade = new List<MotionTarget>();
        private readonly List<float> _cascadeDelay = new List<float>();
        private readonly List<Node> _nodes = new List<Node>();
        private readonly List<StarAnim> _starAnims = new List<StarAnim>();
        private FxLayer _fx;
        private Label _settingsHint;
        private Label _hint;
        private LevelEntry[] _entries;
        private MotionTarget _unlockRing;
        private float _unlockRingAt;
        private float _openedAt;
        private float _lastNow;
        private float _steamDebt;
        private VisualElement _steamSource;
        private MotionTarget _logoSway;
        private MotionTarget[] _bulbs;
        private MotionTarget _playBreath;
        private MotionTarget _aliSway;
        private MotionTarget _marker;
        private int _currentIndex = -1;
        private int _unlockIndex = -1;
        private float _unlockAt;
        private ScrollView _scroll;
        private VisualElement _currentRow;
        private bool _scrollStarted;
        private bool _scrollDone;
        private float _scrollStartAt;
        private float _scrollTarget;
        private float _scrollFrom;
        private VisualElement _card;
        private MotionTarget _cardPanel;
        private float _cardOpenedAt;

        /// <summary>Чи є в меню підсвітка щойно відкритого рівня.</summary>
        public bool HasUnlockRing => _unlockRing != null;

        /// <summary>Підпис кнопки рівня: замок, зірки або просто номер (відкритий, ще не пройдений). Для підказок і тестів.</summary>
        public static string LevelButtonText(ILocalizer text, LevelEntry entry)
        {
            if (!entry.Unlocked)
            {
                return string.Format(text.Get(TextKeys.MenuLevelLocked), entry.Number);
            }

            return entry.BestStars > 0
                ? string.Format(text.Get(TextKeys.MenuLevelStars), entry.Number, entry.BestStars)
                : string.Format(text.Get(TextKeys.MenuLevel), entry.Number);
        }

        protected override void Build()
        {
            UiMotion.ResetPause(); // вихід з паузи зміни не має лишити рух меню зупиненим
            _openedAt = UiMotion.Now;
            _lastNow = _openedAt;
            ILocalizer text = Context.Text;
            Container.style.justifyContent = Justify.FlexStart;
            Container.style.paddingTop = 40f;
            _entries = LevelProgress.Build(Context.Levels, Context.Profile.Data);
            _currentIndex = LevelMapLayout.CurrentIndex(_entries);
            int highlight = Context.TakeUnlockHighlight();
            int gainLevel;
            int gainFrom;
            Context.TakeStarsGain(out gainLevel, out gainFrom);

            int rowCount = _entries.Length;
            int total = 2 + rowCount + 2; // шапка, логотип, рівні, «Грати», «Налаштування»
            int order = 0;

            AddHeader(order++, total);
            AddLogo(text, order++, total);

            string noticeKey = SaveNoticePolicy.KeyOf(Context.Profile.LastNotice);
            if (noticeKey != null)
            {
                UiFactory.Notice(Container, text.Get(noticeKey));
            }

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1;
            _scroll.style.flexShrink = 1;
            _scroll.style.width = Length.Percent(100);
            _scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.contentContainer.style.paddingTop = MarkerSize + 40f;
            _scroll.contentContainer.style.paddingBottom = 30f;
            _scroll.contentContainer.style.alignItems = Align.Center;
            Container.Add(_scroll);

            // Q-05j: рівень 1 знизу, новіші вище - рядки додаємо від останнього до першого.
            for (int p = 0; p < _entries.Length; p++)
            {
                int i = LevelMapLayout.IndexAtRow(p, _entries.Length);
                bool unlocking = _entries[i].Number == highlight && _entries[i].Unlocked;
                int gain = _entries[i].Number == gainLevel ? gainFrom : -1;
                AddLevelRow(text, i, order++, total, unlocking, gain);
            }

            _hint = UiFactory.Notice(Container, string.Empty);
            _hint.style.flexShrink = 0;
            AddPlay(text, order++, total);
            AddSettings(text, order, total);

            _fx = new FxLayer(FxSeed);
            Container.Add(_fx.Root);
            Update();
        }

        private void Update()
        {
            if (Container == null)
            {
                return;
            }

            float now = UiMotion.Now;
            float t = now - _openedAt;
            for (int i = 0; i < _cascade.Count; i++)
            {
                float e = t - _cascadeDelay[i];
                _cascade[i].Set(1f, 1f, 0f, MenuMotion.CascadeOffset(e), MenuMotion.CascadeOpacity(e));
            }

            float sway = MenuMotion.LogoSway(t);
            _logoSway.Set(sway, sway, 0f, 0f, 1f);
            for (int i = 0; i < _bulbs.Length; i++)
            {
                _bulbs[i].Set(1f, 1f, 0f, 0f, MenuMotion.LightOpacity(i, t));
            }

            float breath = MenuMotion.PlayBreath(t);
            _playBreath.Set(breath, breath, 0f, 0f, 1f);
            float aliScale = MenuMotion.LogoSway(t + 0.7f);
            _aliSway.Set(aliScale, aliScale, 0f, 0f, 1f);

            UpdateNodes(now, t);
            UpdateStars(now);
            UpdateRing(now);
            UpdateScroll(now);
            UpdateSteam(now);
            UpdateCard(now);
            _fx.Update();
        }

        // ---------- побудова ----------

        private void AddHeader(int order, int total)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.width = Length.Percent(92);
            header.style.height = AliSize;
            header.style.flexShrink = 0;

            var ali = new VisualElement { name = "menu-mentor", pickingMode = PickingMode.Ignore };
            ali.style.width = AliSize;
            ali.style.height = AliSize;
            ArtLibrary.Apply(ali, ArtLibrary.Ali);
            _aliSway = new MotionTarget(ali);
            header.Add(ali);

            var coins = new VisualElement { name = "menu-coins", pickingMode = PickingMode.Ignore };
            coins.style.flexDirection = FlexDirection.Row;
            coins.style.alignItems = Align.Center;
            coins.style.paddingLeft = 24f;
            coins.style.paddingRight = 32f;
            coins.style.paddingTop = 10f;
            coins.style.paddingBottom = 10f;
            coins.style.backgroundColor = new Color(UiColors.Espresso.r, UiColors.Espresso.g, UiColors.Espresso.b, 0.78f);
            SetRadius(coins, 40f);
            SetBorder(coins, 4f, UiColors.Saffron);
            ArtLibrary.AddIcon(coins, ArtLibrary.Coin, CoinIconSize);
            var amount = new Label(Context.Profile.Data.Coins.ToString());
            amount.style.color = UiColors.Coin;
            amount.style.fontSize = 46f;
            amount.style.unityFontStyleAndWeight = FontStyle.Bold;
            amount.style.marginLeft = 14f;
            amount.style.textShadow = Shadow(2f, 3f);
            coins.Add(amount);
            header.Add(coins);

            Container.Add(header);
            RegisterCascade(header, order, total);
        }

        private void AddLogo(ILocalizer text, int order, int total)
        {
            var wrap = new VisualElement { name = "menu-logo" };
            wrap.style.alignItems = Align.Center;
            wrap.style.flexShrink = 0;

            var lights = new VisualElement { pickingMode = PickingMode.Ignore };
            lights.style.flexDirection = FlexDirection.Row;
            lights.style.marginBottom = 8f;
            _bulbs = new MotionTarget[MenuMotion.LightCount];
            for (int i = 0; i < MenuMotion.LightCount; i++)
            {
                var bulb = new VisualElement { pickingMode = PickingMode.Ignore };
                bulb.style.width = BulbSize;
                bulb.style.height = BulbSize;
                bulb.style.marginLeft = 14f;
                bulb.style.marginRight = 14f;
                bulb.style.backgroundColor = UiColors.Coin;
                SetRadius(bulb, BulbSize * 0.5f);
                lights.Add(bulb);
                _bulbs[i] = new MotionTarget(bulb);
            }

            wrap.Add(lights);
            _steamSource = lights;

            var badge = new VisualElement { name = "menu-logo-badge", pickingMode = PickingMode.Ignore };
            badge.style.backgroundColor = UiColors.Espresso;
            badge.style.paddingLeft = 48f;
            badge.style.paddingRight = 48f;
            badge.style.paddingTop = 6f;
            badge.style.paddingBottom = 22f;
            SetRadius(badge, 44f);
            SetBorder(badge, 8f, UiColors.Saffron);
            badge.style.borderBottomWidth = 14f;
            Label title = UiFactory.Title(badge, text.Get(TextKeys.GameTitle));
            title.style.marginLeft = 0f;
            title.style.marginRight = 0f;
            title.style.textShadow = Shadow(4f, 6f);
            wrap.Add(badge);
            _logoSway = new MotionTarget(badge);

            Container.Add(wrap);
            RegisterCascade(wrap, order, total);
        }

        private void AddLevelRow(ILocalizer text, int index, int order, int total, bool unlocking, int gainFrom)
        {
            LevelEntry entry = _entries[index];
            var row = new VisualElement { name = "level-row-" + entry.Number };
            row.style.alignItems = Align.Center;
            row.style.flexShrink = 0;

            var node = new Node { Index = index, Number = entry.Number };
            node.Wrap = new VisualElement { name = "level-node-wrap-" + entry.Number };
            node.Wrap.style.width = LevelMapLayout.NodeSize;
            node.Wrap.style.height = LevelMapLayout.NodeSize;
            node.Wrap.style.marginLeft = 2f * LevelMapLayout.NodeOffsetX(index);
            node.Motion = new MotionTarget(node.Wrap);

            bool lockedLook = !entry.Unlocked || unlocking;
            int number = entry.Number;
            node.Button = new Button(SfxHub.WithTapSound(() => OnNode(number))) { name = "level-node-" + entry.Number };
            node.Button.text = entry.Number.ToString();
            node.Button.AddToClassList(UiFactory.PressClass);
            node.Button.style.width = LevelMapLayout.NodeSize;
            node.Button.style.height = LevelMapLayout.NodeSize;
            node.Button.style.minHeight = LevelMapLayout.NodeSize;
            node.Button.style.paddingLeft = 0f;
            node.Button.style.paddingRight = 0f;
            node.Button.style.paddingTop = 0f;
            node.Button.style.paddingBottom = 0f;
            node.Button.style.marginLeft = 0f;
            node.Button.style.marginRight = 0f;
            node.Button.style.marginTop = 0f;
            node.Button.style.marginBottom = 0f;
            node.Button.style.fontSize = NodeFontSize;
            node.Button.style.unityFontStyleAndWeight = FontStyle.Bold;
            node.Button.style.unityTextAlign = TextAnchor.MiddleCenter;
            SetRadius(node.Button, LevelMapLayout.NodeSize * 0.5f);
            SetBorder(node.Button, NodeBorder, UiColors.Espresso);
            node.Button.style.borderBottomWidth = NodeBorder * 2f;
            node.Wrap.Add(node.Button);
            ApplyFace(node.Button, !lockedLook);

            node.Lock = new VisualElement { name = "level-lock-" + entry.Number, pickingMode = PickingMode.Ignore };
            node.Lock.style.position = Position.Absolute;
            node.Lock.style.left = (LevelMapLayout.NodeSize - LockSize) * 0.5f;
            node.Lock.style.top = (LevelMapLayout.NodeSize - LockSize) * 0.5f;
            node.Lock.style.width = LockSize;
            node.Lock.style.height = LockSize;
            ArtLibrary.ApplyFixed(node.Lock, ArtLibrary.Lock, LockSize);
            node.Wrap.Add(node.Lock);
            node.LockMotion = new MotionTarget(node.Lock);
            if (!lockedLook)
            {
                node.Lock.style.display = DisplayStyle.None;
            }
            else
            {
                node.Button.text = string.Empty;
            }

            node.Locked = lockedLook;
            node.Unlocking = unlocking;

            if (unlocking)
            {
                AddUnlockRing(node.Button);
                _unlockIndex = _nodes.Count;
                _unlockAt = _openedAt + MenuMotion.CascadeDelay(order, total) + MenuMotion.CascadeItemSec;
            }

            if (index == _currentIndex && entry.Unlocked)
            {
                node.IsCurrent = true;
                var marker = new VisualElement { name = "level-marker", pickingMode = PickingMode.Ignore };
                marker.style.position = Position.Absolute;
                marker.style.left = (LevelMapLayout.NodeSize - MarkerSize) * 0.5f;
                marker.style.top = -(MarkerSize + 14f);
                marker.style.width = MarkerSize;
                marker.style.height = MarkerSize;
                marker.style.backgroundColor = UiColors.Coin;
                SetBorder(marker, 4f, UiColors.Espresso);
                marker.style.rotate = new Rotate(Angle.Degrees(45f));
                node.Wrap.Add(marker);
                _marker = new MotionTarget(marker);
                _currentRow = row;
            }

            row.Add(node.Wrap);
            row.Add(BuildStars(node, entry, gainFrom, order, total));
            if (index > 0)
            {
                AddLink(row, index - 1, _entries[index].Unlocked); // доріжка вниз до попереднього рівня
            }

            _scroll.Add(row);
            _nodes.Add(node);
            RegisterCascade(row, order, total);
        }

        /// <summary>Доріжка-пунктир від вузла <paramref name="from"/> до наступного; жовта, якщо наступний відкритий.</summary>
        private void AddLink(VisualElement row, int from, bool passed)
        {
            var link = new VisualElement { pickingMode = PickingMode.Ignore };
            link.style.alignItems = Align.Center;
            for (int d = 0; d < LevelMapLayout.DotsPerLink; d++)
            {
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.style.width = LevelMapLayout.DotSize;
                dot.style.height = LevelMapLayout.DotSize;
                dot.style.marginTop = LevelMapLayout.DotGap * 0.5f;
                dot.style.marginBottom = LevelMapLayout.DotGap * 0.5f;
                dot.style.marginLeft = 2f * LevelMapLayout.DotOffsetX(from, LevelMapLayout.DotsPerLink - 1 - d);
                dot.style.backgroundColor = passed ? UiColors.Coin : DotOff;
                SetRadius(dot, LevelMapLayout.DotSize * 0.5f);
                link.Add(dot);
            }

            row.Add(link);
        }

        private VisualElement BuildStars(Node node, LevelEntry entry, int gainFrom, int order, int total)
        {
            var stars = new VisualElement { name = "level-stars-" + entry.Number, pickingMode = PickingMode.Ignore };
            stars.style.flexDirection = FlexDirection.Row;
            stars.style.height = StarSize;
            stars.style.marginTop = StarRowGap;
            stars.style.marginLeft = 2f * LevelMapLayout.NodeOffsetX(node.Index);
            if (!entry.Unlocked)
            {
                stars.style.visibility = Visibility.Hidden;
            }

            int best = entry.BestStars;
            float startAt = _openedAt + MenuMotion.CascadeDelay(order, total) + MenuMotion.CascadeItemSec;
            for (int s = 0; s < MaxStars; s++)
            {
                bool newStar = gainFrom >= 0 && s >= gainFrom && s < best && !UiMotion.Reduced;
                bool full = s < best && !newStar;
                var icon = ArtLibrary.AddIcon(stars, full ? ArtLibrary.StarFull : ArtLibrary.StarEmpty, StarSize);
                if (icon == null)
                {
                    continue;
                }

                if (newStar)
                {
                    _starAnims.Add(new StarAnim { Icon = icon, Motion = new MotionTarget(icon), Index = s - gainFrom, StartAt = startAt });
                }
            }

            return stars;
        }

        private void AddUnlockRing(Button button)
        {
            var ring = new VisualElement { name = "unlock-ring", pickingMode = PickingMode.Ignore };
            ring.style.position = Position.Absolute;
            ring.style.left = RingInset;
            ring.style.top = RingInset;
            ring.style.right = RingInset;
            ring.style.bottom = RingInset;
            SetBorder(ring, RingWidth, UiColors.Coin);
            SetRadius(ring, RingRadius);
            button.Add(ring);
            _unlockRing = new MotionTarget(ring);
            _unlockRingAt = UiMotion.Now;
            _unlockRing.Set(1f, 1f, 0f, 0f, 0f);
        }

        private void AddPlay(ILocalizer text, int order, int total)
        {
            var cascade = new VisualElement { name = "menu-play" };
            cascade.style.width = Length.Percent(PlayWidthPercent);
            cascade.style.flexShrink = 0;
            var breath = new VisualElement();
            Button play = UiFactory.AddButton(breath, text.Get(TextKeys.MenuPlay), OnPlayCurrent);
            play.name = "menu-play-button";
            play.style.width = Length.Percent(100);
            breath.style.width = Length.Percent(100);
            cascade.Add(breath);
            _playBreath = new MotionTarget(breath);
            Container.Add(cascade);
            RegisterCascade(cascade, order, total);
        }

        private void AddSettings(ILocalizer text, int order, int total)
        {
            var wrap = new VisualElement { name = "menu-settings" };
            wrap.style.width = Length.Percent(PlayWidthPercent);
            wrap.style.flexShrink = 0;
            Button settings = UiFactory.AddButton(wrap, text.Get(TextKeys.MenuSettings), OnSettings);
            settings.style.width = Length.Percent(100);
            settings.style.height = SmallButtonHeight;
            Container.Add(wrap);
            _settingsHint = UiFactory.Body(Container, string.Empty);
            _settingsHint.style.flexShrink = 0;
            RegisterCascade(wrap, order, total);
        }

        private void RegisterCascade(VisualElement element, int order, int total)
        {
            _cascade.Add(new MotionTarget(element));
            _cascadeDelay.Add(MenuMotion.CascadeDelay(order, total));
        }

        // ---------- кадр ----------

        private void UpdateNodes(float now, float t)
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                Node node = _nodes[i];
                float scale = 1f;
                float shift = 0f;
                if (node.IsCurrent && !node.Locked)
                {
                    scale = MenuMotion.CurrentPulse(t);
                }

                if (node.ShakeAt >= 0f)
                {
                    shift = MenuMotion.LockedShake(now - node.ShakeAt);
                }

                if (node.Unlocking)
                {
                    float e = now - _unlockAt;
                    if (node.Locked && MenuMotion.NodeOpened(e))
                    {
                        node.Locked = false;
                        node.Lock.style.display = DisplayStyle.None;
                        node.Button.text = node.Number.ToString();
                        ApplyFace(node.Button, true);
                        SfxHub.Play(SfxCue.StarChime, 2); // QB-LVL-05: «дзинь» відкриття
                    }

                    if (node.Locked)
                    {
                        float ls = MenuMotion.LockScale(e);
                        node.LockMotion.Set(ls, ls, 0f, 0f, MenuMotion.LockOpacity(e));
                    }

                    scale *= MenuMotion.NodePop(e);
                }

                node.Motion.Set(scale, scale, shift, 0f, 1f);
            }

            if (_marker != null)
            {
                _marker.Set(1f, 1f, 0f, MenuMotion.MarkerBob(t), 1f);
            }
        }

        private void UpdateStars(float now)
        {
            for (int i = 0; i < _starAnims.Count; i++)
            {
                StarAnim star = _starAnims[i];
                float e = now - star.StartAt;
                if (!star.Shown && MenuMotion.StarShown(star.Index, e))
                {
                    star.Shown = true;
                    ArtLibrary.Apply(star.Icon, ArtLibrary.StarFull);
                    SfxHub.Play(SfxCue.StarChime, star.Index); // QB-LVL-06: висота росте із кожною зіркою
                }

                if (star.Shown)
                {
                    float pop = MenuMotion.StarPop(star.Index, e);
                    star.Motion.Set(pop, pop, 0f, 0f, 1f);
                }
            }
        }

        private void UpdateRing(float now)
        {
            if (_unlockRing != null)
            {
                _unlockRing.Set(1f, 1f, 0f, 0f, ResultMotion.UnlockOpacity(now - _unlockRingAt));
            }
        }

        private void UpdateScroll(float now)
        {
            if (_scrollDone || _currentRow == null)
            {
                return;
            }

            if (!_scrollStarted)
            {
                float viewport = _scroll.contentViewport.layout.height;
                float rowY = _currentRow.layout.y;
                float rowH = _currentRow.layout.height;
                float content = _scroll.contentContainer.layout.height;
                if (float.IsNaN(viewport) || float.IsNaN(rowY) || float.IsNaN(content) || viewport < 1f || rowH < 1f)
                {
                    return;
                }

                _scrollTarget = LevelMapLayout.ScrollTarget(rowY, rowH, viewport, content);
                _scrollFrom = Mathf.Max(0f, content - viewport); // карта відкривається знизу (рівень 1)
                _scrollStartAt = now;
                _scrollStarted = true;
            }

            float elapsed = now - _scrollStartAt;
            _scroll.scrollOffset = new Vector2(0f, MenuMotion.ScrollOffset(_scrollFrom, _scrollTarget, elapsed));
            if (UiMotion.Reduced || elapsed >= MenuMotion.ScrollSec)
            {
                _scrollDone = true;
            }
        }

        private void UpdateSteam(float now)
        {
            float dt = Mathf.Clamp(now - _lastNow, 0f, MaxFrameSec);
            _lastNow = now;
            if (UiMotion.Reduced)
            {
                return;
            }

            _steamDebt += MenuMotion.SteamPerSecond * dt;
            while (_steamDebt >= 1f)
            {
                _steamDebt -= 1f;
                _fx.Steam(_steamSource, false);
            }
        }

        private void UpdateCard(float now)
        {
            if (_card == null)
            {
                return;
            }

            float e = now - _cardOpenedAt;
            _cardPanel.Set(1f, 1f, 0f, MenuMotion.CascadeOffset(e * (MenuMotion.CascadeItemSec / CardOpenSec)), MenuMotion.CascadeOpacity(e * (MenuMotion.CascadeItemSec / CardOpenSec)));
        }

        // ---------- ввід ----------

        private void OnNode(int number)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Number != number)
                {
                    continue;
                }

                if (_entries[i].Unlocked)
                {
                    OpenCard(number);
                    return;
                }

                for (int n = 0; n < _nodes.Count; n++)
                {
                    if (_nodes[n].Index == i)
                    {
                        _nodes[n].ShakeAt = UiMotion.Now;
                    }
                }
 // QB-LVL-04: тряска без вібрації
                SfxHub.Play(SfxCue.Locked); // QB-LVL-04: глухий звук
                _hint.text = string.Format(Context.Text.Get(TextKeys.MenuLevelLockedHint), LevelMapLayout.RequiredNumber(_entries, i));
                return;
            }
        }

        private void OnPlayCurrent()
        {
            if (_currentIndex >= 0)
            {
                OnPlay(_entries[_currentIndex].Number);
            }
        }

        private void OnPlay(int levelNumber)
        {
            if (Context.IsLevelUnlocked(levelNumber) && Context.SelectLevel(levelNumber) && Context.Flow.Play())
            {
                Open(GameFlow.SceneOf(Context.Flow.Current));
            }
        }

        private void OnSettings()
        {
            _settingsHint.text = Context.Text.Get(TextKeys.MenuSettingsSoon);
        }

        // ---------- картка рівня (QB-LVL-07) ----------

        private void OpenCard(int number)
        {
            CloseCard();
            LevelDefinition level = Context.Levels.FindByNumber(number);
            if (level == null)
            {
                return;
            }

            ILocalizer text = Context.Text;
            _card = new VisualElement { name = "level-card" };
            _card.style.position = Position.Absolute;
            _card.style.left = 0;
            _card.style.top = 0;
            _card.style.right = 0;
            _card.style.bottom = 0;
            _card.style.backgroundColor = Dim;
            _card.style.alignItems = Align.Center;
            _card.style.justifyContent = Justify.Center;
            _card.style.paddingTop = 260f;
            _card.pickingMode = PickingMode.Position;
            _card.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == _card)
                {
                    CloseCard();
                }
            });

            var panel = new VisualElement { name = "level-card-panel" };
            panel.AddToClassList("ks-panel");
            panel.style.width = Length.Percent(86);
            panel.style.backgroundColor = PanelFace;
            panel.style.paddingLeft = 36f;
            panel.style.paddingRight = 36f;
            panel.style.paddingTop = 30f;
            panel.style.paddingBottom = 40f;
            panel.style.alignItems = Align.Center;
            UiDecor.AddPanelShadow(panel);

            var title = new Label(string.Format(text.Get(TextKeys.MenuLevel), number));
            title.style.fontSize = 56f;
            title.style.color = UiColors.Espresso;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 18f;
            panel.Add(title);

            LevelCardLine[] lines = LevelCard.Lines(level);
            for (int i = 0; i < lines.Length; i++)
            {
                panel.Add(BuildCardRow(text, lines[i], level.Goal.Type));
            }

            Button start = UiFactory.AddButton(panel, text.Get(TextKeys.LevelStart), () => OnPlay(number));
            start.name = "level-card-start";
            start.style.width = Length.Percent(80);
            start.style.marginTop = 36f;

            _card.Add(panel);
            _cardPanel = new MotionTarget(panel);
            _cardOpenedAt = UiMotion.Now;
            Container.Add(_card);
        }

        private static VisualElement BuildCardRow(ILocalizer text, LevelCardLine line, GoalType goal)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.alignSelf = Align.Stretch;
            row.style.marginTop = 10f;

            var icons = new VisualElement { pickingMode = PickingMode.Ignore };
            icons.style.flexDirection = FlexDirection.Row;
            icons.style.width = (3f * LevelIconSize) + 8f;
            icons.style.flexShrink = 0;
            if (line.Stars == 0)
            {
                ArtLibrary.AddIcon(icons, goal == GoalType.Coins ? ArtLibrary.Coin : ArtLibrary.MeatDone, LevelIconSize);
            }
            else
            {
                for (int s = 0; s < line.Stars; s++)
                {
                    ArtLibrary.AddIcon(icons, ArtLibrary.StarFull, LevelIconSize);
                }
            }

            row.Add(icons);
            string body = line.HasNumber ? string.Format(text.Get(line.Key), line.Number) : text.Get(line.Key);
            var label = new Label(body);
            label.style.fontSize = CardFontSize;
            label.style.color = UiColors.Espresso;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 1;
            label.style.flexGrow = 1;
            label.style.marginLeft = 14f;
            row.Add(label);
            return row;
        }

        private void CloseCard()
        {
            if (_card != null)
            {
                _card.RemoveFromHierarchy();
                _card = null;
                _cardPanel = null;
            }
        }

        // ---------- стиль ----------

        private static void ApplyFace(Button button, bool open)
        {
            button.style.backgroundColor = open ? UiColors.Saffron : LockedFace;
            button.style.color = open ? UiColors.Espresso : LockedText;
        }

        private static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }

        private static void SetBorder(VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth = w;
            e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w;
            e.style.borderRightWidth = w;
            e.style.borderTopColor = c;
            e.style.borderBottomColor = c;
            e.style.borderLeftColor = c;
            e.style.borderRightColor = c;
        }

        private static TextShadow Shadow(float dx, float dy)
        {
            return new TextShadow { offset = new Vector2(dx, dy), blurRadius = 0f, color = new Color(0.1f, 0.06f, 0.03f) };
        }

        private sealed class Node
        {
            public int Index;
            public int Number;
            public VisualElement Wrap;
            public Button Button;
            public MotionTarget Motion;
            public VisualElement Lock;
            public MotionTarget LockMotion;
            public bool Locked;
            public bool Unlocking;
            public bool IsCurrent;
            public float ShakeAt = -1f;
        }

        private sealed class StarAnim
        {
            public VisualElement Icon;
            public MotionTarget Motion;
            public int Index;
            public float StartAt;
            public bool Shown;
        }
    }
}
