using System;
using System.Collections.Generic;
using UnityEngine;
using PerfectParcel.Storage;

namespace PerfectParcel.Localization
{
    /// <summary>
    /// Менеджер локалізації гри на 6 мов (EN, UA, PL, DE, ES, RU).
    /// Забезпечує автовизначення мови при першому запуску, ручне перемикання
    /// та швидкий доступ до локалізованих рядків інтерфейсу без сторонніх залежностей.
    /// </summary>
    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }

        public const string LangEN = "en";
        public const string LangUA = "ua";
        public const string LangPL = "pl";
        public const string LangDE = "de";
        public const string LangES = "es";
        public const string LangRU = "ru";

        public static readonly string[] SupportedLanguages = { LangEN, LangUA, LangPL, LangDE, LangES, LangRU };

        public string CurrentLanguage { get; private set; } = LangEN;
        public event Action<string> OnLanguageChanged;

        private Dictionary<string, Dictionary<string, string>> _translations;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeTranslations();
                ResolveLanguage();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void ResolveLanguage()
        {
            string savedLang = SaveManager.Instance != null ? SaveManager.Instance.GetLanguage() : "";

            if (!string.IsNullOrEmpty(savedLang) && IsLanguageSupported(savedLang))
            {
                CurrentLanguage = savedLang;
            }
            else
            {
                // Автовизначення системної мови
                CurrentLanguage = DetectSystemLanguage();
                SaveManager.Instance?.SetLanguage(CurrentLanguage);
            }

            Debug.Log($"[LocalizationManager] Активна мова інтерфейсу: {CurrentLanguage}");
        }

        public static string DetectSystemLanguage()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Ukrainian:
                    return LangUA;
                case SystemLanguage.Polish:
                    return LangPL;
                case SystemLanguage.German:
                    return LangDE;
                case SystemLanguage.Spanish:
                    return LangES;
                case SystemLanguage.Russian:
                case SystemLanguage.Belarusian:
                    return LangRU;
                default:
                    return LangEN;
            }
        }

        public void SetLanguage(string langCode)
        {
            if (!IsLanguageSupported(langCode)) langCode = LangEN;

            if (CurrentLanguage != langCode)
            {
                CurrentLanguage = langCode;
                SaveManager.Instance?.SetLanguage(langCode);
                OnLanguageChanged?.Invoke(CurrentLanguage);
                Debug.Log($"[LocalizationManager] Мову змінено на: {CurrentLanguage}");
            }
        }

        public bool IsLanguageSupported(string langCode)
        {
            if (string.IsNullOrEmpty(langCode)) return false;
            foreach (var lang in SupportedLanguages)
            {
                if (lang.Equals(langCode, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";

            if (_translations.TryGetValue(CurrentLanguage, out var langDict) && langDict.TryGetValue(key, out var val))
            {
                return val;
            }

            // Фолбек на англійську
            if (_translations.TryGetValue(LangEN, out var enDict) && enDict.TryGetValue(key, out var enVal))
            {
                return enVal;
            }

            return key;
        }

        public string Get(string key, params object[] args)
        {
            string raw = Get(key);
            try
            {
                return string.Format(raw, args);
            }
            catch
            {
                return raw;
            }
        }

        private void InitializeTranslations()
        {
            _translations = new Dictionary<string, Dictionary<string, string>>
            {
                [LangEN] = new Dictionary<string, string>
                {
                    // General & Menu
                    ["app_name"] = "PERFECT PARCEL",
                    ["btn_play"] = "▶️ PLAY",
                    ["btn_warehouse"] = "🛠️ WAREHOUSE (SHOP)",
                    ["btn_settings"] = "⚙️ SETTINGS",
                    ["btn_daily_rewards"] = "🎁 DAILY REWARD",
                    ["btn_sponsor_chest"] = "📦 SPONSOR CHEST (+250 💰)",
                    ["btn_back"] = "⬅️ Back",
                    ["btn_menu"] = "MENU",
                    ["btn_next_level"] = "NEXT LEVEL ➔",
                    ["btn_retry"] = "↺ RETRY",
                    ["btn_buy"] = "Buy",
                    ["btn_max"] = "MAX",
                    ["level_select_title"] = "SELECT LEVEL (1–30)",
                    ["level_label"] = "Level {0}",
                    ["locked"] = "LOCKED",
                    ["coins"] = "Coins: {0}",
                    ["score"] = "Score: {0}",

                    // Gameplay HUD
                    ["chute_left"] = "⬅️ LEFT (Blue)",
                    ["chute_straight"] = "⬆️ STRAIGHT (Green)",
                    ["chute_right"] = "➡️ RIGHT (Red)",
                    ["lives"] = "Lives",
                    ["combo"] = "COMBO x{0}",

                    // Results
                    ["result_win_title"] = "🎉 LEVEL COMPLETED!",
                    ["result_fail_title"] = "💔 LEVEL FAILED!",
                    ["result_fail_desc"] = "Out of lives or parcel broke. Try again!",
                    ["btn_double_coins"] = "🎬 Double Coins x2 (Video)",

                    // Shop & Upgrades
                    ["shop_screen_title"] = "WAREHOUSE & SHOP",
                    ["shop_get_coins_title"] = "💎 GET COINS (IAP & REWARDED ADS)",
                    ["shop_deficit_desc"] = "Standard earnings cover only ~15% of upgrades.\nGet additional coins to expand your warehouse!",
                    ["shop_free_video"] = "🎬 Sponsor Chest: +250 coins (Video Ad)",
                    ["shop_pack_1"] = "📦 Starter Pack (+5,000 💰)",
                    ["shop_pack_2"] = "🚚 Popular Freight (+15,000 💰 +20%)",
                    ["shop_pack_3"] = "🏦 Tycoon Vault (+40,000 💰 +60% BEST VALUE)",
                    ["shop_no_ads_active"] = "🛡️ No-Ads Privilege: ACTIVE (Interstitials disabled)",
                    ["shop_no_ads_info"] = "💡 Any coin pack permanently disables forced ads (No-Ads).",
                    ["upgrades_section_title"] = "🛠️ WAREHOUSE UPGRADES (7 BRANCHES)",
                    ["upgrade_tier"] = "Tier {0}/5",

                    // Upgrade Names & Descs
                    ["upg_smooth_belt"] = "Smooth Belt",
                    ["upg_smooth_belt_desc"] = "Reduces conveyor speed spikes (-4% speed per tier).",
                    ["upg_safety_buffer"] = "Safety Buffer",
                    ["upg_safety_buffer_desc"] = "Expands deadline buffer at drop line (+0.3s per tier).",
                    ["upg_magnetic_guides"] = "Magnetic Guides",
                    ["upg_magnetic_guides_desc"] = "Broadens swipe sorting tolerance hitbox (+5% per tier).",
                    ["upg_auto_scanner"] = "Auto-Scanner",
                    ["upg_auto_scanner_desc"] = "Chance to auto-reveal mystery packages (+15% per tier).",
                    ["upg_coin_multiplier"] = "Coin Multiplier",
                    ["upg_coin_multiplier_desc"] = "Passive bonus coins on all deliveries (+8% per tier).",
                    ["upg_cushioned_bin"] = "Cushioned Bin",
                    ["upg_cushioned_bin_desc"] = "Reduces life loss penalty for fragile packages.",
                    ["upg_express_fee"] = "Express Delivery Fee",
                    ["upg_express_fee_desc"] = "Bonus coins for sorting urgent packages (+25% per tier).",

                    // Daily Rewards
                    ["daily_reward_title"] = "🎁 7-DAY LOGIN REWARDS",
                    ["daily_day_num"] = "Day {0}",
                    ["daily_claim_btn"] = "CLAIM {0} 💰",
                    ["daily_claim_double_btn"] = "🎬 CLAIM 2X ({0} 💰)",
                    ["daily_already_claimed"] = "Claimed! Next in {0}",
                    ["daily_streak"] = "Day {0} of 7",

                    // Settings
                    ["settings_title"] = "SETTINGS",
                    ["settings_music"] = "Music Volume: {0}%",
                    ["settings_sfx"] = "SFX Volume: {0}%",
                    ["settings_haptics"] = " Vibration (Haptics)",
                    ["settings_language"] = "Language:",
                    ["settings_reset_btn"] = "⚠️ Reset All Progress",
                    ["settings_version"] = "Version: v1.0.0-MVP (Unity 2022.3 LTS)\nPERFECT PARCEL"
                },

                [LangUA] = new Dictionary<string, string>
                {
                    ["app_name"] = "PERFECT PARCEL",
                    ["btn_play"] = "▶️ ГРАТИ",
                    ["btn_warehouse"] = "🛠️ СКЛАД (МАГАЗИН)",
                    ["btn_settings"] = "⚙️ НАЛАШТУВАННЯ",
                    ["btn_daily_rewards"] = "🎁 ЩОДЕННА НАГОРОДА",
                    ["btn_sponsor_chest"] = "📦 СКРИНЯ СПОНСОРА (+250 💰)",
                    ["btn_back"] = "⬅️ Назад",
                    ["btn_menu"] = "МЕНЮ",
                    ["btn_next_level"] = "НАСТУПНИЙ РІВЕНЬ ➔",
                    ["btn_retry"] = "↺ СПРОБУВАТИ ЗНОВУ",
                    ["btn_buy"] = "Купити",
                    ["btn_max"] = "МАКС",
                    ["level_select_title"] = "ВИБІР РІВНІВ (1–30)",
                    ["level_label"] = "Рівень {0}",
                    ["locked"] = "ЗАКРИТО",
                    ["coins"] = "Монети: {0}",
                    ["score"] = "Рахунок: {0}",

                    ["chute_left"] = "⬅️ ВЛІВО (Синій)",
                    ["chute_straight"] = "⬆️ ПРЯМО (Зелений)",
                    ["chute_right"] = "➡️ ВПРАВО (Червоний)",
                    ["lives"] = "Життя",
                    ["combo"] = "КОМБО x{0}",

                    ["result_win_title"] = "🎉 РІВЕНЬ ПРОЙДЕНО!",
                    ["result_fail_title"] = "💔 ПОРАЗКА!",
                    ["result_fail_desc"] = "Усі життя вичерпано або посилку розбито. Спробуйте ще раз!",
                    ["btn_double_coins"] = "🎬 Подвоїти виграш x2 (Відео)",

                    ["shop_screen_title"] = "СКЛАД ТА МАГАЗИН",
                    ["shop_get_coins_title"] = "💎 ОТРИМАТИ МОНЕТИ (МАГАЗИН ТА РЕКЛАМА)",
                    ["shop_deficit_desc"] = "Звичайного заробітку вистачає лише на ~15% покращень.\nОтримайте додаткові монети для розвитку!",
                    ["shop_free_video"] = "🎬 Скриня спонсора: +250 монет (Відео)",
                    ["shop_pack_1"] = "📦 Стартовий пакунок (+5,000 💰)",
                    ["shop_pack_2"] = "🚚 Популярний вантаж (+15,000 💰 +20%)",
                    ["shop_pack_3"] = "🏦 Складський магнат (+40,000 💰 +60% BEST VALUE)",
                    ["shop_no_ads_active"] = "🛡️ Статус No-Ads: АКТИВОВАНО (Рекламу відключено)",
                    ["shop_no_ads_info"] = "💡 Будь-яка покупка назавжди відключає примусову рекламу (No-Ads).",
                    ["upgrades_section_title"] = "🛠️ СКЛАДСЬКІ ПОКРАЩЕННЯ (7 ГІЛОК)",
                    ["upgrade_tier"] = "Рівень {0}/5",

                    ["upg_smooth_belt"] = "Плавний конвеєр",
                    ["upg_smooth_belt_desc"] = "Знижує швидкість конвеєра (-4% за рівень).",
                    ["upg_safety_buffer"] = "Буфер безпеки",
                    ["upg_safety_buffer_desc"] = "Розширює тайм-аут дедлайну лінії падіння (+0.3с за рівень).",
                    ["upg_magnetic_guides"] = "Магнітний сортувальник",
                    ["upg_magnetic_guides_desc"] = "Розширює зону толерантності свайпу (+5% за рівень).",
                    ["upg_auto_scanner"] = "Швидкий авто-сканер",
                    ["upg_auto_scanner_desc"] = "Шанс авто-сканування спец-посилок (+15% за рівень).",
                    ["upg_coin_multiplier"] = "Золотий бонус",
                    ["upg_coin_multiplier_desc"] = "Додатковий дохід із посилок (+8% за рівень).",
                    ["upg_cushioned_bin"] = "М'яка подушка",
                    ["upg_cushioned_bin_desc"] = "Знижує втрату життів від крихких посилок.",
                    ["upg_express_fee"] = "Преміальний тариф",
                    ["upg_express_fee_desc"] = "Бонус за успішні Термінові посилки (+25% за рівень).",

                    ["daily_reward_title"] = "🎁 7-ДЕННІ НАГОРОДИ ЗА ВХІД",
                    ["daily_day_num"] = "День {0}",
                    ["daily_claim_btn"] = "ЗАБРАТИ {0} 💰",
                    ["daily_claim_double_btn"] = "🎬 ЗАБРАТИ 2X ({0} 💰)",
                    ["daily_already_claimed"] = "Отримано! Наступна через {0}",
                    ["daily_streak"] = "День {0} із 7",

                    ["settings_title"] = "НАЛАШТУВАННЯ",
                    ["settings_music"] = "Гучність музики: {0}%",
                    ["settings_sfx"] = "Гучність звуків (SFX): {0}%",
                    ["settings_haptics"] = " Вібрація (Haptics)",
                    ["settings_language"] = "Мова інтерфейсу:",
                    ["settings_reset_btn"] = "⚠️ Скинути весь прогрес",
                    ["settings_version"] = "Версія: v1.0.0-MVP (Unity 2022.3 LTS)\nPERFECT PARCEL"
                },

                [LangPL] = new Dictionary<string, string>
                {
                    ["app_name"] = "PERFECT PARCEL",
                    ["btn_play"] = "▶️ GRAJ",
                    ["btn_warehouse"] = "🛠️ MAGAZYN (SKLEP)",
                    ["btn_settings"] = "⚙️ USTAWIENIA",
                    ["btn_daily_rewards"] = "🎁 NAGRODA DZIENNA",
                    ["btn_sponsor_chest"] = "📦 SKRZYNIA SPONSORA (+250 💰)",
                    ["btn_back"] = "⬅️ Wstecz",
                    ["btn_menu"] = "MENU",
                    ["btn_next_level"] = "NASTĘPNY POZIOM ➔",
                    ["btn_retry"] = "↺ POWTÓRZ",
                    ["btn_buy"] = "Kup",
                    ["btn_max"] = "MAX",
                    ["level_select_title"] = "WYBÓR POZIOMU (1–30)",
                    ["level_label"] = "Poziom {0}",
                    ["locked"] = "ZABLOKOWANE",
                    ["coins"] = "Monety: {0}",
                    ["score"] = "Wynik: {0}",

                    ["chute_left"] = "⬅️ W LEWO (Niebieski)",
                    ["chute_straight"] = "⬆️ PROSTO (Zielony)",
                    ["chute_right"] = "➡️ W PRAWO (Czerwony)",
                    ["lives"] = "Życia",
                    ["combo"] = "KOMBO x{0}",

                    ["result_win_title"] = "🎉 POZIOM UKOŃCZONY!",
                    ["result_fail_title"] = "💔 PORAŻKA!",
                    ["result_fail_desc"] = "Stracono wszystkie życia. Spróbuj ponownie!",
                    ["btn_double_coins"] = "🎬 Podwój monety x2 (Wideo)",

                    ["shop_screen_title"] = "MAGAZYN I SKLEP",
                    ["shop_get_coins_title"] = "💎 ZDOBĄDŹ MONETY (IAP I REKLAMY)",
                    ["shop_deficit_desc"] = "Standardowe nagrody pokrywają tylko ~15% ulepszeń.\nZdobądź monety na rozbudowę magazynu!",
                    ["shop_free_video"] = "🎬 Skrzynia sponsora: +250 monet (Wideo)",
                    ["shop_pack_1"] = "📦 Pakiet startowy (+5,000 💰)",
                    ["shop_pack_2"] = "🚚 Popularny ładunek (+15,000 💰 +20%)",
                    ["shop_pack_3"] = "🏦 Magnat magazynowy (+40,000 💰 +60% BEST VALUE)",
                    ["shop_no_ads_active"] = "🛡️ Status No-Ads: AKTYWNY (Reklamy wyłączone)",
                    ["shop_no_ads_info"] = "💡 Każdy zakup wyłącza wymuszone reklamy (No-Ads).",
                    ["upgrades_section_title"] = "🛠️ ULEPSZENIA MAGAZYNU (7 GAŁĘZI)",
                    ["upgrade_tier"] = "Poziom {0}/5",

                    ["upg_smooth_belt"] = "Płynny taśmociąg",
                    ["upg_smooth_belt_desc"] = "Zmniejsza prędkość taśmociągu (-4% na poziom).",
                    ["upg_safety_buffer"] = "Bufor bezpieczeństwa",
                    ["upg_safety_buffer_desc"] = "Wydłuża czas przed linią spadku (+0.3s na poziom).",
                    ["upg_magnetic_guides"] = "Prowadnice magnetyczne",
                    ["upg_magnetic_guides_desc"] = "Zwiększa tolerancję strefy sortowania (+5% na poziom).",
                    ["upg_auto_scanner"] = "Szybki autoskaner",
                    ["upg_auto_scanner_desc"] = "Szansa na automatyczne skanowanie paczek specjalnych.",
                    ["upg_coin_multiplier"] = "Złoty mnożnik",
                    ["upg_coin_multiplier_desc"] = "Dodatkowe monety za wszystkie paczki (+8% na poziom).",
                    ["upg_cushioned_bin"] = "Miękki pojemnik",
                    ["upg_cushioned_bin_desc"] = "Chroni delikatne paczki przed stłuczeniem.",
                    ["upg_express_fee"] = "Taryfa ekspresowa",
                    ["upg_express_fee_desc"] = "Bonus za paczki pilne (+25% na poziom).",

                    ["daily_reward_title"] = "🎁 7-DNIOWE NAGRODY DZIENNE",
                    ["daily_day_num"] = "Dzień {0}",
                    ["daily_claim_btn"] = "ODBIERZ {0} 💰",
                    ["daily_claim_double_btn"] = "🎬 ODBIERZ 2X ({0} 💰)",
                    ["daily_already_claimed"] = "Odebrano! Następna za {0}",
                    ["daily_streak"] = "Dzień {0} z 7",

                    ["settings_title"] = "USTAWIENIA",
                    ["settings_music"] = "Głośność muzyki: {0}%",
                    ["settings_sfx"] = "Głośność dźwięków: {0}%",
                    ["settings_haptics"] = " Wibracje (Haptics)",
                    ["settings_language"] = "Język:",
                    ["settings_reset_btn"] = "⚠️ Zresetuj cały postęp",
                    ["settings_version"] = "Wersja: v1.0.0-MVP (Unity 2022.3 LTS)\nPERFECT PARCEL"
                },

                [LangDE] = new Dictionary<string, string>
                {
                    ["app_name"] = "PERFECT PARCEL",
                    ["btn_play"] = "▶️ SPIELEN",
                    ["btn_warehouse"] = "🛠️ LAGER (SHOP)",
                    ["btn_settings"] = "⚙️ EINSTELLUNGEN",
                    ["btn_daily_rewards"] = "🎁 TÄGLICHE BELOHNUNG",
                    ["btn_sponsor_chest"] = "📦 SPONSORENTRUHE (+250 💰)",
                    ["btn_back"] = "⬅️ Zurück",
                    ["btn_menu"] = "MENÜ",
                    ["btn_next_level"] = "NÄCHSTES LEVEL ➔",
                    ["btn_retry"] = "↺ WIEDERHOLEN",
                    ["btn_buy"] = "Kaufen",
                    ["btn_max"] = "MAX",
                    ["level_select_title"] = "LEVELAUSWAHL (1–30)",
                    ["level_label"] = "Level {0}",
                    ["locked"] = "GESPERRT",
                    ["coins"] = "Münzen: {0}",
                    ["score"] = "Punkte: {0}",

                    ["chute_left"] = "⬅️ LINKS (Blau)",
                    ["chute_straight"] = "⬆️ GERADE (Grün)",
                    ["chute_right"] = "➡️ RECHTS (Rot)",
                    ["lives"] = "Leben",
                    ["combo"] = "KOMBO x{0}",

                    ["result_win_title"] = "🎉 LEVEL GESCHAFFT!",
                    ["result_fail_title"] = "💔 LEVEL FEHLGESCHLAGEN!",
                    ["result_fail_desc"] = "Alle Leben verloren. Versuche es erneut!",
                    ["btn_double_coins"] = "🎬 Münzen verdoppeln x2 (Video)",

                    ["shop_screen_title"] = "LAGER & SHOP",
                    ["shop_get_coins_title"] = "💎 MÜNZEN ERHALTEN (IAP & ADS)",
                    ["shop_deficit_desc"] = "Normale Einnahmen decken nur ~15% der Upgrades.\nHole dir Münzen zum Ausbau deines Lagers!",
                    ["shop_free_video"] = "🎬 Sponsorentruhe: +250 Münzen (Video)",
                    ["shop_pack_1"] = "📦 Starter-Paket (+5.000 💰)",
                    ["shop_pack_2"] = "🚚 Beliebte Fracht (+15.000 💰 +20%)",
                    ["shop_pack_3"] = "🏦 Lager-Tycoon (+40.000 💰 +60% BEST VALUE)",
                    ["shop_no_ads_active"] = "🛡️ No-Ads Status: AKTIV (Keine Werbeunterbrechungen)",
                    ["shop_no_ads_info"] = "💡 Jeder Kauf deaktiviert erzwungene Werbung dauerhaft.",
                    ["upgrades_section_title"] = "🛠️ LAGER-UPGRADES (7 ZWEIGE)",
                    ["upgrade_tier"] = "Stufe {0}/5",

                    ["upg_smooth_belt"] = "Sanftes Förderband",
                    ["upg_smooth_belt_desc"] = "Verringert die Bandgeschwindigkeit (-4% pro Stufe).",
                    ["upg_safety_buffer"] = "Sicherheitspuffer",
                    ["upg_safety_buffer_desc"] = "Verlängert die Reaktionszeit an der Abwurflinie.",
                    ["upg_magnetic_guides"] = "Magnetische Führungen",
                    ["upg_magnetic_guides_desc"] = "Vergrößert die Sortier-Trefferzone (+5% pro Stufe).",
                    ["upg_auto_scanner"] = "Schnell-Autoscanner",
                    ["upg_auto_scanner_desc"] = "Chance auf automatisches Scannen von Spezialpaketen.",
                    ["upg_coin_multiplier"] = "Münz-Multiplikator",
                    ["upg_coin_multiplier_desc"] = "Zusätzliche Münzen für alle Lieferungen (+8% pro Stufe).",
                    ["upg_cushioned_bin"] = "Gepolsterte Box",
                    ["upg_cushioned_bin_desc"] = "Schützt zerbrechliche Pakete vor Beschädigung.",
                    ["upg_express_fee"] = "Express-Zuschlag",
                    ["upg_express_fee_desc"] = "Bonus für eilige Pakete (+25% pro Stufe).",

                    ["daily_reward_title"] = "🎁 7-TAGE LOGIN-BELOHNUNGEN",
                    ["daily_day_num"] = "Tag {0}",
                    ["daily_claim_btn"] = "EINFORDERN {0} 💰",
                    ["daily_claim_double_btn"] = "🎬 DOPPELT EINFORDERN ({0} 💰)",
                    ["daily_already_claimed"] = "Eingefordert! Nächste in {0}",
                    ["daily_streak"] = "Tag {0} von 7",

                    ["settings_title"] = "EINSTELLUNGEN",
                    ["settings_music"] = "Musiklautstärke: {0}%",
                    ["settings_sfx"] = "Effektlautstärke: {0}%",
                    ["settings_haptics"] = " Vibration (Haptik)",
                    ["settings_language"] = "Sprache:",
                    ["settings_reset_btn"] = "⚠️ Gesamten Fortschritt zurücksetzen",
                    ["settings_version"] = "Version: v1.0.0-MVP (Unity 2022.3 LTS)\nPERFECT PARCEL"
                },

                [LangES] = new Dictionary<string, string>
                {
                    ["app_name"] = "PERFECT PARCEL",
                    ["btn_play"] = "▶️ JUGAR",
                    ["btn_warehouse"] = "🛠️ ALMACÉN (TIENDA)",
                    ["btn_settings"] = "⚙️ AJUSTES",
                    ["btn_daily_rewards"] = "🎁 RECOMPENSA DIARIA",
                    ["btn_sponsor_chest"] = "📦 COFRE DEL PATROCINADOR (+250 💰)",
                    ["btn_back"] = "⬅️ Atrás",
                    ["btn_menu"] = "MENÚ",
                    ["btn_next_level"] = "SIGUIENTE NIVEL ➔",
                    ["btn_retry"] = "↺ REINTENTAR",
                    ["btn_buy"] = "Comprar",
                    ["btn_max"] = "MÁX",
                    ["level_select_title"] = "SELECCIONAR NIVEL (1–30)",
                    ["level_label"] = "Nivel {0}",
                    ["locked"] = "BLOQUEADO",
                    ["coins"] = "Monedas: {0}",
                    ["score"] = "Puntos: {0}",

                    ["chute_left"] = "⬅️ IZQUIERDA (Azul)",
                    ["chute_straight"] = "⬆️ RECTO (Verde)",
                    ["chute_right"] = "➡️ DERECHA (Rojo)",
                    ["lives"] = "Vidas",
                    ["combo"] = "COMBO x{0}",

                    ["result_win_title"] = "🎉 ¡NIVEL COMPLETADO!",
                    ["result_fail_title"] = "💔 ¡NIVEL FALLIDO!",
                    ["result_fail_desc"] = "Te has quedado sin vidas. ¡Inténtalo de nuevo!",
                    ["btn_double_coins"] = "🎬 Duplicar monedas x2 (Vídeo)",

                    ["shop_screen_title"] = "ALMACÉN Y TIENDA",
                    ["shop_get_coins_title"] = "💎 OBTENER MONEDAS (IAP Y ANUNCIOS)",
                    ["shop_deficit_desc"] = "Las ganancias estándar solo cubren ~15% de mejoras.\n¡Consigue monedas para expandir tu almacén!",
                    ["shop_free_video"] = "🎬 Cofre patrocinado: +250 monedas (Vídeo)",
                    ["shop_pack_1"] = "📦 Paquete Inicial (+5.000 💰)",
                    ["shop_pack_2"] = "🚚 Carga Popular (+15.000 💰 +20%)",
                    ["shop_pack_3"] = "🏦 Magnate de Almacén (+40.000 💰 +60% BEST VALUE)",
                    ["shop_no_ads_active"] = "🛡️ Estado Sin Anuncios: ACTIVO",
                    ["shop_no_ads_info"] = "💡 Cualquier compra desactiva permanentemente anuncios molestos.",
                    ["upgrades_section_title"] = "🛠️ MEJORAS DE ALMACÉN (7 RAMAS)",
                    ["upgrade_tier"] = "Nivel {0}/5",

                    ["upg_smooth_belt"] = "Cinta Suave",
                    ["upg_smooth_belt_desc"] = "Reduce la velocidad de la cinta (-4% por nivel).",
                    ["upg_safety_buffer"] = "Margen de Seguridad",
                    ["upg_safety_buffer_desc"] = "Amplía el tiempo de reacción al final de la cinta.",
                    ["upg_magnetic_guides"] = "Guías Magnéticas",
                    ["upg_magnetic_guides_desc"] = "Amplía la zona de tolerancia de deslizamiento (+5%).",
                    ["upg_auto_scanner"] = "Escáner Rápido",
                    ["upg_auto_scanner_desc"] = "Probabilidad de escanear paquetes especiales automáticamente.",
                    ["upg_coin_multiplier"] = "Multiplicador de Oro",
                    ["upg_coin_multiplier_desc"] = "Monedas adicionales en todas las entregas (+8%).",
                    ["upg_cushioned_bin"] = "Contenedor Acolchado",
                    ["upg_cushioned_bin_desc"] = "Reduce la penalización de vida en paquetes frágiles.",
                    ["upg_express_fee"] = "Tarifa Express",
                    ["upg_express_fee_desc"] = "Bono por paquetes urgentes clasificados (+25%).",

                    ["daily_reward_title"] = "🎁 RECOMPENSAS DIARIAS (7 DÍAS)",
                    ["daily_day_num"] = "Día {0}",
                    ["daily_claim_btn"] = "RECLAMAR {0} 💰",
                    ["daily_claim_double_btn"] = "🎬 RECLAMAR 2X ({0} 💰)",
                    ["daily_already_claimed"] = "¡Reclamado! Siguiente en {0}",
                    ["daily_streak"] = "Día {0} de 7",

                    ["settings_title"] = "AJUSTES",
                    ["settings_music"] = "Volumen de música: {0}%",
                    ["settings_sfx"] = "Volumen de efectos: {0}%",
                    ["settings_haptics"] = " Vibración (Háptica)",
                    ["settings_language"] = "Idioma:",
                    ["settings_reset_btn"] = "⚠️ Restablecer todo el progreso",
                    ["settings_version"] = "Versión: v1.0.0-MVP (Unity 2022.3 LTS)\nPERFECT PARCEL"
                },

                [LangRU] = new Dictionary<string, string>
                {
                    ["app_name"] = "PERFECT PARCEL",
                    ["btn_play"] = "▶️ ИГРАТЬ",
                    ["btn_warehouse"] = "🛠️ СКЛАД (МАГАЗИН)",
                    ["btn_settings"] = "⚙️ НАСТРОЙКИ",
                    ["btn_daily_rewards"] = "🎁 ЕЖЕДНЕВНАЯ НАГРАДА",
                    ["btn_sponsor_chest"] = "📦 СУНДУК СПОНСОРА (+250 💰)",
                    ["btn_back"] = "⬅️ Назад",
                    ["btn_menu"] = "МЕНЮ",
                    ["btn_next_level"] = "СЛЕДУЮЩИЙ УРОВЕНЬ ➔",
                    ["btn_retry"] = "↺ ПОВТОРИТЬ",
                    ["btn_buy"] = "Купить",
                    ["btn_max"] = "МАКС",
                    ["level_select_title"] = "ВЫБОР УРОВНЯ (1–30)",
                    ["level_label"] = "Уровень {0}",
                    ["locked"] = "ЗАКРЫТО",
                    ["coins"] = "Монеты: {0}",
                    ["score"] = "Счет: {0}",

                    ["chute_left"] = "⬅️ ВЛЕВО (Синий)",
                    ["chute_straight"] = "⬆️ ПРЯМО (Зеленый)",
                    ["chute_right"] = "➡️ ВПРАВО (Красный)",
                    ["lives"] = "Жизни",
                    ["combo"] = "КОМБО x{0}",

                    ["result_win_title"] = "🎉 УРОВЕНЬ ПРОЙДЕН!",
                    ["result_fail_title"] = "💔 ПОРАЖЕНИЕ!",
                    ["result_fail_desc"] = "Жизни исчерпаны или посылка разбита. Попробуйте снова!",
                    ["btn_double_coins"] = "🎬 Удвоить монеты x2 (Видео)",

                    ["shop_screen_title"] = "СКЛАД И МАГАЗИН",
                    ["shop_get_coins_title"] = "💎 ПОЛУЧИТЬ МОНЕТЫ (IAP И РЕКЛАМА)",
                    ["shop_deficit_desc"] = "Обычного дохода хватает лишь на ~15% улучшений.\nПолучите дополнительные монеты для развития склада!",
                    ["shop_free_video"] = "🎬 Сундук спонсора: +250 монет (Видео)",
                    ["shop_pack_1"] = "📦 Стартовый набор (+5,000 💰)",
                    ["shop_pack_2"] = "🚚 Популярный груз (+15,000 💰 +20%)",
                    ["shop_pack_3"] = "🏦 Складской магнат (+40,000 💰 +60% BEST VALUE)",
                    ["shop_no_ads_active"] = "🛡️ Статус No-Ads: АКТИВИРОВАН (Реклама отключена)",
                    ["shop_no_ads_info"] = "💡 Любая покупка навсегда отключает межстраничную рекламу.",
                    ["upgrades_section_title"] = "🛠️ УЛУЧШЕНИЯ СКЛАДА (7 ВЕТОК)",
                    ["upgrade_tier"] = "Уровень {0}/5",

                    ["upg_smooth_belt"] = "Плавный конвейер",
                    ["upg_smooth_belt_desc"] = "Снижает скорость конвейера (-4% за уровень).",
                    ["upg_safety_buffer"] = "Буфер безопасности",
                    ["upg_safety_buffer_desc"] = "Увеличивает тайм-аут дедлайна у линии падения (+0.3с).",
                    ["upg_magnetic_guides"] = "Магнитные направляющие",
                    ["upg_magnetic_guides_desc"] = "Расширяет зону толерантности свайпа (+5% за уровень).",
                    ["upg_auto_scanner"] = "Быстрый авто-сканер",
                    ["upg_auto_scanner_desc"] = "Шанс мгновенного сканирования спец-посылок (+15%).",
                    ["upg_coin_multiplier"] = "Золотой бонус",
                    ["upg_coin_multiplier_desc"] = "Дополнительный доход со всех посылок (+8% за уровень).",
                    ["upg_cushioned_bin"] = "Мягкая подушка",
                    ["upg_cushioned_bin_desc"] = "Снижает потерю жизней при падении хрупких посылок.",
                    ["upg_express_fee"] = "Премиальный тариф",
                    ["upg_express_fee_desc"] = "Бонус за успешные срочные посылки (+25% за уровень).",

                    ["daily_reward_title"] = "🎁 7-ДНЕВНЫЕ НАГРАДЫ ЗА ВХОД",
                    ["daily_day_num"] = "День {0}",
                    ["daily_claim_btn"] = "ЗАБРАТЬ {0} 💰",
                    ["daily_claim_double_btn"] = "🎬 ЗАБРАТЬ 2X ({0} 💰)",
                    ["daily_already_claimed"] = "Получено! Следующая через {0}",
                    ["daily_streak"] = "День {0} из 7",

                    ["settings_title"] = "НАСТРОЙКИ",
                    ["settings_music"] = "Громкость музыки: {0}%",
                    ["settings_sfx"] = "Громкость звуков (SFX): {0}%",
                    ["settings_haptics"] = " Вибрация (Haptics)",
                    ["settings_language"] = "Язык интерфейса:",
                    ["settings_reset_btn"] = "⚠️ Сбросить весь прогресс",
                    ["settings_version"] = "Версия: v1.0.0-MVP (Unity 2022.3 LTS)\nPERFECT PARCEL"
                }
            };
        }
    }
}
