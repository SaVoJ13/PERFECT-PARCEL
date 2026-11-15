using KebabStreet.Core;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>Головне меню: вибір рівня (кнопка на кожен рівень каталогу), "Налаштування" (заглушка) і повідомлення про стан збереження.</summary>
    public sealed class MenuController : ScreenController
    {
        private const float RingInset = -10f;
        private const float RingWidth = 6f;
        private const float RingRadius = 28f;

        private Label _settingsHint;
        private MotionTarget _unlockRing;
        private float _unlockRingAt;

        protected override void Build()
        {
            UiMotion.ResetPause(); // вихід з паузи зміни не має лишити рух меню зупиненим
            ILocalizer text = Context.Text;
            UiFactory.Title(Container, text.Get(TextKeys.GameTitle));

            string noticeKey = SaveNoticePolicy.KeyOf(Context.Profile.LastNotice);
            if (noticeKey != null)
            {
                UiFactory.Notice(Container, text.Get(noticeKey));
            }

            int highlight = Context.TakeUnlockHighlight();
            LevelEntry[] entries = LevelProgress.Build(Context.Levels, Context.Profile.Data);
            for (int i = 0; i < entries.Length; i++)
            {
                LevelEntry entry = entries[i];
                int number = entry.Number;
                Button button = UiFactory.AddButton(Container, LevelButtonText(text, entry), () => OnPlay(number));
                button.SetEnabled(entry.Unlocked);
                AddLevelIcons(button, entry);
                if (entry.Number == highlight && entry.Unlocked)
                {
                    AddUnlockRing(button);
                }
            }

            UiFactory.AddButton(Container, text.Get(TextKeys.MenuSettings), OnSettings);
            _settingsHint = UiFactory.Body(Container, string.Empty);
        }

        /// <summary>Q-05e (QB-RES-08): рамка-підсвітка щойно відкритого рівня, пульсує, доки гравець не покине меню.</summary>
        private void AddUnlockRing(Button button)
        {
            var ring = new VisualElement { name = "unlock-ring", pickingMode = PickingMode.Ignore };
            ring.style.position = Position.Absolute;
            ring.style.left = RingInset;
            ring.style.top = RingInset;
            ring.style.right = RingInset;
            ring.style.bottom = RingInset;
            ring.style.borderTopWidth = RingWidth;
            ring.style.borderBottomWidth = RingWidth;
            ring.style.borderLeftWidth = RingWidth;
            ring.style.borderRightWidth = RingWidth;
            ring.style.borderTopColor = UiColors.Coin;
            ring.style.borderBottomColor = UiColors.Coin;
            ring.style.borderLeftColor = UiColors.Coin;
            ring.style.borderRightColor = UiColors.Coin;
            ring.style.borderTopLeftRadius = RingRadius;
            ring.style.borderTopRightRadius = RingRadius;
            ring.style.borderBottomLeftRadius = RingRadius;
            ring.style.borderBottomRightRadius = RingRadius;
            button.Add(ring);
            _unlockRing = new MotionTarget(ring);
            _unlockRingAt = UiMotion.Now;
            _unlockRing.Set(1f, 1f, 0f, 0f, 0f);
        }

        /// <summary>Чи є в меню підсвітка щойно відкритого рівня.</summary>
        public bool HasUnlockRing => _unlockRing != null;

        private void Update()
        {
            if (_unlockRing != null)
            {
                _unlockRing.Set(1f, 1f, 0f, 0f, ResultMotion.UnlockOpacity(UiMotion.Now - _unlockRingAt));
            }
        }

        private const float LevelIconSize = 40f;
        private const float LevelIconMargin = 20f;
        private const int MaxStars = 3;

        /// <summary>Замок або зірки на кнопці рівня (S1-18, S1-23: біля лівого краю, текст не перекривається).</summary>
        private static void AddLevelIcons(Button button, LevelEntry entry)
        {
            if (!entry.Unlocked)
            {
                ArtLibrary.AddLeadingIcons(button, new[] { ArtLibrary.Lock }, LevelIconSize, LevelIconMargin);
                return;
            }

            if (entry.BestStars > 0)
            {
                var names = new string[MaxStars];
                for (int s = 0; s < MaxStars; s++)
                {
                    names[s] = s < entry.BestStars ? ArtLibrary.StarFull : ArtLibrary.StarEmpty;
                }

                ArtLibrary.AddLeadingIcons(button, names, LevelIconSize, LevelIconMargin);
            }
        }

        /// <summary>Підпис кнопки рівня: замок, зірки або просто номер (відкритий, ще не пройдений).</summary>
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
    }
}
