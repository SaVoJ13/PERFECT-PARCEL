using System;
using UnityEngine;
using PerfectParcel.Conveyor;
using PerfectParcel.Core;
using PerfectParcel.Economy;
using PerfectParcel.Levels;
using PerfectParcel.Localization;
using PerfectParcel.Monetization;
using PerfectParcel.Sorting;
using PerfectParcel.Storage;

namespace PerfectParcel.UI
{
    /// <summary>
    /// Головний менеджер користувацького інтерфейсу (UIManager).
    /// Забезпечує навігацію та повноцінну інтерактивність усіх 6 обов'язкових екранів гри,
    /// підтримку модалу 7-денних нагород, інтеграцію AdMob / IAP та локалізацію на 6 мов (EN, UA, PL, DE, ES, RU).
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("State")]
        [SerializeField] private ScreenType currentScreen = ScreenType.MainMenu;

        [Header("References")]
        [SerializeField] private SwipeInputHandler inputHandler;

        // Runtime gameplay data
        private int _score;
        private int _coins;
        private int _sortedCount;
        private int _targetCount = 15;
        private int _lives = 3;
        private int _combo = 0;
        private bool _isLastLevelVictory = false;

        private Vector2 _levelSelectScroll;
        private Vector2 _upgradesScroll;

        private string _shopStatusMessage = "";
        private float _shopStatusTimer = 0f;
        private bool _showDailyRewardModal = false;

        public ScreenType CurrentScreen => currentScreen;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ScoreChangedEvent>(OnScoreChanged);
            EventBus.Subscribe<LivesChangedEvent>(OnLivesChanged);
            EventBus.Subscribe<ParcelSortedEvent>(OnParcelSorted);
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ScoreChangedEvent>(OnScoreChanged);
            EventBus.Unsubscribe<LivesChangedEvent>(OnLivesChanged);
            EventBus.Unsubscribe<ParcelSortedEvent>(OnParcelSorted);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        public void Initialize(SwipeInputHandler input)
        {
            inputHandler = input;
        }

        private void Update()
        {
            if (_shopStatusTimer > 0f)
            {
                _shopStatusTimer -= Time.deltaTime;
                if (_shopStatusTimer <= 0f)
                {
                    _shopStatusMessage = "";
                }
            }
        }

        public void SwitchScreen(ScreenType newScreen)
        {
            Audio.AudioManager.Instance?.PlaySFX(Audio.SoundEffect.ButtonClick);
            currentScreen = newScreen;
            _showDailyRewardModal = false;
            Debug.Log($"[UIManager] Перехід на екран: {newScreen}");
        }

        private string T(string key) => LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key) : key;
        private string T(string key, params object[] args) => LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key, args) : key;

        #region Event Handlers
        private void OnScoreChanged(ScoreChangedEvent e)
        {
            _score = e.CurrentScore;
            _coins = e.CurrentCoins;
            _sortedCount = e.SortedCount;
            _targetCount = e.TargetCount;
        }

        private void OnLivesChanged(LivesChangedEvent e)
        {
            _lives = e.CurrentLives;
        }

        private void OnParcelSorted(ParcelSortedEvent e)
        {
            _combo = e.CurrentCombo;
        }

        private void OnGameStateChanged(GameStateChangedEvent e)
        {
            if (e.NewState == GameState.LevelComplete)
            {
                _isLastLevelVictory = true;
                int stars = (_lives >= 3) ? 3 : (_lives >= 2) ? 2 : 1;
                int currentLvl = GameManager.Instance != null ? GameManager.Instance.CurrentLevelIndex : 1;
                SaveManager.Instance?.SaveLevelStars(currentLvl, stars, _score);
                SaveManager.Instance?.AddCoins(_coins);
                AchievementsManager.Instance?.CheckLevelProgressionAchievements(currentLvl, stars);

                SwitchScreen(ScreenType.Results);
            }
            else if (e.NewState == GameState.GameOver)
            {
                _isLastLevelVictory = false;
                SwitchScreen(ScreenType.Results);
            }
        }
        #endregion

        #region OnGUI Master Window Rendering
        private void OnGUI()
        {
            float scale = Screen.width / 540f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            switch (currentScreen)
            {
                case ScreenType.MainMenu:
                    RenderMainMenuScreen();
                    break;
                case ScreenType.LevelSelect:
                    RenderLevelSelectScreen();
                    break;
                case ScreenType.Gameplay:
                    RenderGameplayScreen(scale);
                    break;
                case ScreenType.Results:
                    RenderResultsScreen(scale);
                    break;
                case ScreenType.Upgrades:
                    RenderUpgradesScreen();
                    break;
                case ScreenType.Settings:
                    RenderSettingsScreen();
                    break;
            }

            if (_showDailyRewardModal)
            {
                RenderDailyRewardModal();
            }
        }
        #endregion

        #region Screen 1: Main Menu
        private void RenderMainMenuScreen()
        {
            var headerStyle = new GUIStyle(GUI.skin.box) { fontSize = 16 };
            GUILayout.BeginArea(new Rect(20, 20, 500, 70), headerStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("📦 " + T("app_name"), new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold });
            GUILayout.FlexibleSpace();
            int coins = SaveManager.Instance != null ? SaveManager.Instance.Coins : 0;
            GUILayout.Label($"💰 {coins}", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold });
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            float centerY = 310f;
            GUILayout.BeginArea(new Rect(60, centerY, 420, 560));

            // Кнопка PLAY
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
            if (GUILayout.Button(T("btn_play"), GUILayout.Height(80)))
            {
                SwitchScreen(ScreenType.LevelSelect);
            }

            // Кнопка WAREHOUSE / SHOP
            GUI.backgroundColor = new Color(0.95f, 0.65f, 0.1f);
            GUILayout.Space(15);
            if (GUILayout.Button(T("btn_warehouse"), GUILayout.Height(70)))
            {
                SwitchScreen(ScreenType.Upgrades);
            }

            // Кнопка DAILY REWARD
            bool hasDailyAvailable = DailyRewardManager.Instance != null && DailyRewardManager.Instance.IsRewardAvailableToday;
            GUI.backgroundColor = hasDailyAvailable ? new Color(1f, 0.85f, 0.2f) : new Color(0.5f, 0.5f, 0.6f);
            GUILayout.Space(15);
            string dailyBtnText = hasDailyAvailable ? $"🎁 {T("btn_daily_rewards")} (⭐ НОВИЙ!)" : $"🎁 {T("btn_daily_rewards")}";
            if (GUILayout.Button(dailyBtnText, GUILayout.Height(65)))
            {
                _showDailyRewardModal = true;
            }

            // Кнопка SPONSOR CHEST
            GUI.backgroundColor = new Color(1f, 0.85f, 0.2f);
            GUILayout.Space(15);
            if (GUILayout.Button(T("btn_sponsor_chest"), GUILayout.Height(65)))
            {
                AdsManager.Instance?.ShowRewardedAd(() =>
                {
                    SaveManager.Instance?.AddCoins(250);
                    _shopStatusMessage = "🎁 " + T("shop_free_video") + " (+250 💰)!";
                    _shopStatusTimer = 3.5f;
                }, AdsManager.PlacementSponsorChest);
            }

            // Кнопка SETTINGS
            GUI.backgroundColor = new Color(0.3f, 0.6f, 0.95f);
            GUILayout.Space(15);
            if (GUILayout.Button(T("btn_settings"), GUILayout.Height(65)))
            {
                SwitchScreen(ScreenType.Settings);
            }

            if (!string.IsNullOrEmpty(_shopStatusMessage))
            {
                GUILayout.Space(10);
                GUILayout.Label($"<b>{_shopStatusMessage}</b>", new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.yellow } });
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndArea();
        }
        #endregion

        #region Screen 2: Level Select (1..30)
        private void RenderLevelSelectScreen()
        {
            RenderTopBar(T("level_select_title"), ScreenType.MainMenu);

            _levelSelectScroll = GUILayout.BeginScrollView(_levelSelectScroll, GUILayout.Width(520), GUILayout.Height(760));
            int totalStars = SaveManager.Instance != null ? SaveManager.Instance.GetTotalStars() : 0;
            GUILayout.Label($"⭐️ {totalStars} / 90", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
            GUILayout.Space(10);

            int columns = 5;
            int totalLevels = 30;

            for (int i = 1; i <= totalLevels; i += columns)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < columns && (i + c) <= totalLevels; c++)
                {
                    int lvlIndex = i + c;
                    bool isUnlocked = SaveManager.Instance != null && SaveManager.Instance.IsLevelUnlocked(lvlIndex);
                    int stars = SaveManager.Instance != null ? SaveManager.Instance.GetLevelStars(lvlIndex) : 0;

                    GUI.backgroundColor = isUnlocked ? new Color(0.2f, 0.8f, 0.4f) : new Color(0.4f, 0.4f, 0.4f);

                    string starText = stars > 0 ? new string('★', stars) : (isUnlocked ? "NEW" : "🔒");
                    string btnLabel = $"{lvlIndex}\n{starText}";

                    GUI.enabled = isUnlocked;
                    if (GUILayout.Button(btnLabel, GUILayout.Width(92), GUILayout.Height(75)))
                    {
                        StartLevelFromUI(lvlIndex);
                    }
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndScrollView();
        }

        private void StartLevelFromUI(int levelIndex)
        {
            SwitchScreen(ScreenType.Gameplay);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartLevel(levelIndex);
            }
        }
        #endregion

        #region Screen 3: Gameplay HUD
        private void RenderGameplayScreen(float scale)
        {
            int currentLvl = GameManager.Instance != null ? GameManager.Instance.CurrentLevelIndex : 1;

            // Верхня панель HUD
            GUILayout.BeginArea(new Rect(15, 15, 510, 65), GUI.skin.box);
            GUILayout.BeginHorizontal();

            GUILayout.Label($"<b>{T("level_label", currentLvl)}</b>", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold });
            GUILayout.FlexibleSpace();
            GUILayout.Label(GetHeartsString(_lives), new GUIStyle(GUI.skin.label) { fontSize = 18 });
            GUILayout.FlexibleSpace();
            GUILayout.Label($"💰 {_coins}", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Прогрес та комбо
            GUILayout.BeginArea(new Rect(15, 85, 510, 50));
            GUILayout.BeginHorizontal();
            GUILayout.Label(T("score", _score), new GUIStyle(GUI.skin.label) { fontSize = 15 });
            GUILayout.FlexibleSpace();
            if (_combo > 1)
            {
                GUILayout.Label(T("combo", _combo), new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.yellow } });
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"📦 {_sortedCount} / {_targetCount}", new GUIStyle(GUI.skin.label) { fontSize = 15 });
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Кнопки доступності One-Thumb сортування
            float bottomY = (Screen.height / scale) - 100f;
            GUILayout.BeginArea(new Rect(15, bottomY, 510, 85));
            GUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.55f, 0.95f); // 🔵 Лівий (Коло)
            if (GUILayout.Button(T("chute_left"), GUILayout.Height(75)))
            {
                inputHandler?.TriggerDirectionFromUI(SortingDirection.Left);
            }

            GUI.backgroundColor = new Color(0.06f, 0.75f, 0.45f); // 🟢 Зелений (Трикутник)
            if (GUILayout.Button(T("chute_straight"), GUILayout.Height(75)))
            {
                inputHandler?.TriggerDirectionFromUI(SortingDirection.Straight);
            }

            GUI.backgroundColor = new Color(0.95f, 0.25f, 0.25f); // 🔴 Червоний (Ромб)
            if (GUILayout.Button(T("chute_right"), GUILayout.Height(75)))
            {
                inputHandler?.TriggerDirectionFromUI(SortingDirection.Right);
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
        #endregion

        #region Screen 4: Results (Win / Fail)
        private void RenderResultsScreen(float scale)
        {
            float boxWidth = 460;
            float boxHeight = 440;
            float x = (540 - boxWidth) / 2;
            float y = (Screen.height / scale - boxHeight) / 2;

            int currentLvl = GameManager.Instance != null ? GameManager.Instance.CurrentLevelIndex : 1;
            string title = _isLastLevelVictory ? T("result_win_title") : T("result_fail_title");
            Color titleColor = _isLastLevelVictory ? Color.green : Color.red;

            GUI.Box(new Rect(x, y, boxWidth, boxHeight), "");
            GUILayout.BeginArea(new Rect(x + 20, y + 20, boxWidth - 40, boxHeight - 40));

            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = titleColor } };
            GUILayout.Label(title, titleStyle);
            GUILayout.Space(10);

            if (_isLastLevelVictory)
            {
                int stars = (_lives >= 3) ? 3 : (_lives >= 2) ? 2 : 1;
                string starIcons = stars == 3 ? "⭐️ ⭐️ ⭐️" : stars == 2 ? "⭐️ ⭐️" : "⭐️";
                GUILayout.Label(starIcons, new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter });
                GUILayout.Label($"{T("score", _score)}\n{T("coins", "+" + _coins)}", new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
                GUILayout.Space(15);

                // Rewarded Ads Button (x2 Coins)
                GUI.backgroundColor = new Color(1f, 0.8f, 0.1f);
                if (GUILayout.Button(T("btn_double_coins"), GUILayout.Height(55)))
                {
                    AdsManager.Instance?.ShowRewardedAd(() =>
                    {
                        SaveManager.Instance?.AddCoins(_coins);
                        _coins *= 2;
                        Debug.Log("[UIManager] Rewarded Ads переглянуто! Монети подвоєно.");
                    }, AdsManager.PlacementDoubleWin);
                }

                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
                GUILayout.Space(10);
                if (GUILayout.Button(T("btn_next_level"), GUILayout.Height(55)))
                {
                    AdsManager.Instance?.RecordLevelCompleted(currentLvl, () =>
                    {
                        StartLevelFromUI(currentLvl + 1);
                    });
                }
            }
            else
            {
                GUILayout.Label(T("result_fail_desc"), new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
                GUILayout.Space(25);

                GUI.backgroundColor = new Color(0.95f, 0.4f, 0.2f);
                if (GUILayout.Button(T("btn_retry"), GUILayout.Height(65)))
                {
                    StartLevelFromUI(currentLvl);
                }
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(10);
            if (GUILayout.Button(T("btn_menu"), GUILayout.Height(45)))
            {
                SwitchScreen(ScreenType.MainMenu);
            }

            GUILayout.EndArea();
        }
        #endregion

        #region Screen 5: Warehouse Upgrades & Coin Shop
        private void RenderUpgradesScreen()
        {
            RenderTopBar(T("shop_screen_title"), ScreenType.MainMenu);

            _upgradesScroll = GUILayout.BeginScrollView(_upgradesScroll, GUILayout.Width(520), GUILayout.Height(780));

            // Повідомлення про покупку або перегляд реклами
            if (!string.IsNullOrEmpty(_shopStatusMessage))
            {
                GUI.backgroundColor = new Color(0.2f, 0.9f, 0.5f);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"<b>{_shopStatusMessage}</b>", new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter });
                GUILayout.EndVertical();
                GUI.backgroundColor = Color.white;
                GUILayout.Space(10);
            }

            // 1. Блок подолання дефіциту: Магазин монет та реклама (ADR-008)
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(T("shop_get_coins_title"), new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
            GUILayout.Label(T("shop_deficit_desc"), new GUIStyle(GUI.skin.label) { fontSize = 12 });
            GUILayout.Space(8);

            // Скриня спонсора (Rewarded Video)
            GUI.backgroundColor = new Color(1f, 0.85f, 0.2f);
            if (GUILayout.Button(T("shop_free_video"), GUILayout.Height(48)))
            {
                AdsManager.Instance?.ShowRewardedAd(() =>
                {
                    SaveManager.Instance?.AddCoins(250);
                    _shopStatusMessage = "🎉 " + T("shop_free_video") + " (+250 💰)!";
                    _shopStatusTimer = 3.5f;
                }, AdsManager.PlacementSponsorChest);
            }

            GUILayout.Space(8);
            GUILayout.Label("<b>Google Play Billing (IAP):</b>", new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold });

            // Пакет 1: Starter Pack ($0.99)
            GUI.backgroundColor = new Color(0.85f, 0.95f, 1f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(T("shop_pack_1") + "\n<i>pp_coins_tier1_5k</i>", new GUIStyle(GUI.skin.label) { fontSize = 13 });
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("$0.99 (~40 ₴)", GUILayout.Width(130), GUILayout.Height(42)))
            {
                SaveManager.Instance?.RecordIAPPurchase("pp_coins_tier1_5k", 5000);
                _shopStatusMessage = "✅ " + T("shop_pack_1") + " OK!";
                _shopStatusTimer = 3.5f;
            }
            GUILayout.EndHorizontal();

            // Пакет 2: Popular Pack ($2.49)
            GUILayout.BeginHorizontal();
            GUILayout.Label(T("shop_pack_2") + "\n<i>pp_coins_tier2_15k</i>", new GUIStyle(GUI.skin.label) { fontSize = 13 });
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("$2.49 (~100 ₴)", GUILayout.Width(130), GUILayout.Height(42)))
            {
                SaveManager.Instance?.RecordIAPPurchase("pp_coins_tier2_15k", 15000);
                _shopStatusMessage = "✅ " + T("shop_pack_2") + " OK!";
                _shopStatusTimer = 3.5f;
            }
            GUILayout.EndHorizontal();

            // Пакет 3: Tycoon Pack ($4.99)
            GUI.backgroundColor = new Color(1f, 0.9f, 0.6f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(T("shop_pack_3") + "\n<i>pp_coins_tier3_40k</i>", new GUIStyle(GUI.skin.label) { fontSize = 13 });
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("$4.99 (~200 ₴)", GUILayout.Width(130), GUILayout.Height(42)))
            {
                SaveManager.Instance?.RecordIAPPurchase("pp_coins_tier3_40k", 40000);
                _shopStatusMessage = "✅ " + T("shop_pack_3") + " OK!";
                _shopStatusTimer = 3.5f;
            }
            GUILayout.EndHorizontal();

            GUI.backgroundColor = Color.white;
            bool hasNoAds = SaveManager.Instance?.CurrentData != null && SaveManager.Instance.CurrentData.hasPurchasedAnyIAP;
            string noAdsText = hasNoAds ? T("shop_no_ads_active") : T("shop_no_ads_info");
            GUILayout.Label(noAdsText, new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = hasNoAds ? Color.green : Color.white } });

            GUILayout.EndVertical();
            GUILayout.Space(15);

            // 2. Блок складських покращень
            GUILayout.Label(T("upgrades_section_title"), new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
            GUILayout.Space(5);

            foreach (UpgradeType type in Enum.GetValues(typeof(UpgradeType)))
            {
                int tier = UpgradesManager.Instance != null ? UpgradesManager.Instance.GetUpgradeTier(type) : 0;
                int price = UpgradesManager.Instance != null ? UpgradesManager.Instance.GetUpgradePrice(type) : -1;
                bool canAfford = UpgradesManager.Instance != null && UpgradesManager.Instance.CanAfford(type);

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{UpgradesManager.GetUpgradeName(type)}</b> ({T("upgrade_tier", tier)})", new GUIStyle(GUI.skin.label) { fontSize = 16 });
                GUILayout.FlexibleSpace();

                if (tier >= UpgradesManager.MaxUpgradeTier)
                {
                    GUILayout.Label($"<b>{T("btn_max")}</b>", new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.green } });
                }
                else
                {
                    GUI.backgroundColor = canAfford ? Color.green : Color.gray;
                    if (GUILayout.Button($"{T("btn_buy")}: 💰 {price}", GUILayout.Width(140), GUILayout.Height(40)))
                    {
                        UpgradesManager.Instance?.PurchaseUpgrade(type);
                    }
                    GUI.backgroundColor = Color.white;
                }
                GUILayout.EndHorizontal();

                GUILayout.Label(UpgradesManager.GetUpgradeDescription(type), new GUIStyle(GUI.skin.label) { fontSize = 13 });
                GUILayout.EndVertical();
                GUILayout.Space(5);
            }

            GUILayout.EndScrollView();
        }
        #endregion

        #region Screen 6: Settings Modal
        private void RenderSettingsScreen()
        {
            RenderTopBar(T("settings_title"), ScreenType.MainMenu);

            GUILayout.BeginArea(new Rect(30, 110, 480, 720));

            var save = SaveManager.Instance?.CurrentData;
            if (save != null)
            {
                GUILayout.Label(T("settings_music", Mathf.RoundToInt(save.settings.musicVolume * 100)), new GUIStyle(GUI.skin.label) { fontSize = 16 });
                float newMusic = GUILayout.HorizontalSlider(save.settings.musicVolume, 0f, 1f);
                if (Mathf.Abs(newMusic - save.settings.musicVolume) > 0.01f)
                {
                    save.settings.musicVolume = newMusic;
                    Audio.AudioManager.Instance?.SetMusicVolume(newMusic);
                }
                GUILayout.Space(15);

                GUILayout.Label(T("settings_sfx", Mathf.RoundToInt(save.settings.sfxVolume * 100)), new GUIStyle(GUI.skin.label) { fontSize = 16 });
                float newSfx = GUILayout.HorizontalSlider(save.settings.sfxVolume, 0f, 1f);
                if (Mathf.Abs(newSfx - save.settings.sfxVolume) > 0.01f)
                {
                    save.settings.sfxVolume = newSfx;
                    Audio.AudioManager.Instance?.SetSfxVolume(newSfx);
                }
                GUILayout.Space(15);

                save.settings.hapticsEnabled = GUILayout.Toggle(save.settings.hapticsEnabled, T("settings_haptics"), new GUIStyle(GUI.skin.toggle) { fontSize = 16 });
                GUILayout.Space(25);

                // Вибір із 6 мов (EN, UA, PL, DE, ES, RU)
                GUILayout.Label(T("settings_language"), new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("🇬🇧 English", GUILayout.Height(45))) LocalizationManager.Instance?.SetLanguage(LocalizationManager.LangEN);
                if (GUILayout.Button("🇺🇦 Українська", GUILayout.Height(45))) LocalizationManager.Instance?.SetLanguage(LocalizationManager.LangUA);
                if (GUILayout.Button("🇵🇱 Polski", GUILayout.Height(45))) LocalizationManager.Instance?.SetLanguage(LocalizationManager.LangPL);
                GUILayout.EndHorizontal();

                GUILayout.Space(8);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("🇩🇪 Deutsch", GUILayout.Height(45))) LocalizationManager.Instance?.SetLanguage(LocalizationManager.LangDE);
                if (GUILayout.Button("🇪🇸 Español", GUILayout.Height(45))) LocalizationManager.Instance?.SetLanguage(LocalizationManager.LangES);
                if (GUILayout.Button("🌐 Русский", GUILayout.Height(45))) LocalizationManager.Instance?.SetLanguage(LocalizationManager.LangRU);
                GUILayout.EndHorizontal();

                GUILayout.Space(40);

                // Кнопка очищення даних (Reset)
                GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f);
                if (GUILayout.Button(T("settings_reset_btn"), GUILayout.Height(50)))
                {
                    SaveManager.Instance?.ResetAllProgress();
                }
                GUI.backgroundColor = Color.white;

                GUILayout.Space(20);
                GUILayout.Label(T("settings_version"), new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12 });
            }

            GUILayout.EndArea();
        }
        #endregion

        #region Daily Rewards Modal
        private void RenderDailyRewardModal()
        {
            GUI.Box(new Rect(0, 0, 540, 1920), ""); // Фон затемнення

            float boxWidth = 490;
            float boxHeight = 620;
            float x = (540 - boxWidth) / 2;
            float y = (960 - boxHeight) / 2;

            GUI.Box(new Rect(x, y, boxWidth, boxHeight), "", GUI.skin.window);
            GUILayout.BeginArea(new Rect(x + 15, y + 15, boxWidth - 30, boxHeight - 30));

            GUILayout.Label(T("daily_reward_title"), new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.yellow } });
            GUILayout.Space(8);

            var mgr = DailyRewardManager.Instance;
            int currentStreak = mgr != null ? mgr.CurrentStreakDay : 1;
            bool canClaim = mgr != null && mgr.IsRewardAvailableToday;

            GUILayout.Label(T("daily_streak", currentStreak), new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(10);

            // 7 карток днів
            for (int day = 1; day <= 7; day++)
            {
                int reward = mgr != null ? mgr.GetRewardForDay(day) : 100;
                bool isPast = day < currentStreak || (day == currentStreak && !canClaim);
                bool isCurrent = day == currentStreak && canClaim;

                if (isCurrent) GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
                else if (isPast) GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
                else GUI.backgroundColor = new Color(0.25f, 0.3f, 0.4f);

                GUILayout.BeginHorizontal(GUI.skin.box);
                string dayLabel = T("daily_day_num", day);
                string status = isPast ? "✅" : isCurrent ? "⭐ ДІЮЧИЙ" : "🔒";
                string grand = (day == 7) ? " 🏆" : "";
                GUILayout.Label($"<b>{dayLabel}</b>{grand}", new GUIStyle(GUI.skin.label) { fontSize = 14 });
                GUILayout.FlexibleSpace();
                GUILayout.Label($"💰 +{reward}  {status}", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
                GUILayout.EndHorizontal();
                GUILayout.Space(3);
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(12);

            if (canClaim)
            {
                int baseReward = mgr.GetCurrentAvailableReward();

                GUI.backgroundColor = new Color(1f, 0.85f, 0.15f);
                if (GUILayout.Button(T("daily_claim_double_btn", baseReward * 2), GUILayout.Height(52)))
                {
                    AdsManager.Instance?.ShowRewardedAd(() =>
                    {
                        int granted = mgr.ClaimDailyReward(true);
                        _shopStatusMessage = $"🎉 Отримано щоденну нагороду 2X: +{granted} монет!";
                        _shopStatusTimer = 3.5f;
                        _showDailyRewardModal = false;
                    }, AdsManager.PlacementDailyReward);
                }

                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
                GUILayout.Space(6);
                if (GUILayout.Button(T("daily_claim_btn", baseReward), GUILayout.Height(44)))
                {
                    int granted = mgr.ClaimDailyReward(false);
                    _shopStatusMessage = $"🎁 Отримано щоденну нагороду: +{granted} монет!";
                    _shopStatusTimer = 3.5f;
                    _showDailyRewardModal = false;
                }
            }
            else
            {
                TimeSpan remaining = mgr != null ? mgr.GetTimeUntilNextDayUtc() : TimeSpan.Zero;
                string timeStr = $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
                GUILayout.Label(T("daily_already_claimed", timeStr), new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.yellow } });
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(8);
            if (GUILayout.Button("✕ " + T("btn_back"), GUILayout.Height(38)))
            {
                _showDailyRewardModal = false;
            }

            GUILayout.EndArea();
        }
        #endregion

        private void RenderTopBar(string title, ScreenType backScreen)
        {
            GUILayout.BeginArea(new Rect(15, 15, 510, 60), GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("btn_back"), GUILayout.Width(90), GUILayout.Height(45)))
            {
                SwitchScreen(backScreen);
            }
            GUILayout.Space(10);
            GUILayout.Label(title, new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft });
            GUILayout.FlexibleSpace();
            int coins = SaveManager.Instance != null ? SaveManager.Instance.Coins : 0;
            GUILayout.Label($"💰 {coins}", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight });
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUILayout.Space(70);
        }

        private string GetHeartsString(int lives)
        {
            string s = "";
            for (int i = 0; i < lives; i++) s += "❤️ ";
            return string.IsNullOrEmpty(s) ? "💔 (0)" : s;
        }
    }
}
