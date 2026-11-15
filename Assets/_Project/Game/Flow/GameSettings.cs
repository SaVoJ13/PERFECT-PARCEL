using System;

namespace KebabStreet.Game
{
    /// <summary>Сховище налаштувань гри (Q-05h): ціле за ключем. У грі - PlayerPrefs, у тестах - пам'ять. Не схема збереження.</summary>
    public interface ISettingsStore
    {
        int GetInt(string key, int fallback);

        void SetInt(string key, int value);

        /// <summary>Скидає записане на диск.</summary>
        void Flush();
    }

    /// <summary>Сховище в пам'яті (тести, запасний варіант без PlayerPrefs).</summary>
    public sealed class MemorySettingsStore : ISettingsStore
    {
        private readonly System.Collections.Generic.Dictionary<string, int> _values =
            new System.Collections.Generic.Dictionary<string, int>();

        /// <summary>Скільки разів викликано <see cref="Flush"/>.</summary>
        public int Flushes { get; private set; }

        public int GetInt(string key, int fallback)
        {
            int value;
            return _values.TryGetValue(key, out value) ? value : fallback;
        }

        public void SetInt(string key, int value)
        {
            _values[key] = value;
        }

        public void Flush()
        {
            Flushes++;
        }
    }

    /// <summary>Сховище на <c>PlayerPrefs</c> Unity (налаштування пристрою, поза файлом збереження гри).</summary>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        public int GetInt(string key, int fallback)
        {
            return UnityEngine.PlayerPrefs.GetInt(key, fallback);
        }

        public void SetInt(string key, int value)
        {
            UnityEngine.PlayerPrefs.SetInt(key, value);
        }

        public void Flush()
        {
            UnityEngine.PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Налаштування гравця (Q-05h): музика, звуки, вібрація, «менше анімацій». Живуть у <c>PlayerPrefs</c>, а не в
    /// схемі збереження гри. <see cref="ApplyTo"/> передає їх у <see cref="SfxRouter"/> (шини «Ефекти» і «Музика»)
    /// і в <see cref="UiMotion.Reduced"/>; вібрація лише зберігається (<see cref="HapticsEnabled"/>), сам сервіс - Q-05i.
    /// Усе за замовчуванням увімкнене, крім «менше анімацій».
    /// </summary>
    public sealed class GameSettings
    {
        public const string MusicKey = "ks.settings.music";
        public const string SoundsKey = "ks.settings.sounds";
        public const string HapticsKey = "ks.settings.haptics";
        public const string ReducedKey = "ks.settings.reduced_motion";

        private static GameSettings _shared;

        private readonly ISettingsStore _store;

        public GameSettings(ISettingsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            MusicOn = _store.GetInt(MusicKey, 1) != 0;
            SoundsOn = _store.GetInt(SoundsKey, 1) != 0;
            HapticsEnabled = _store.GetInt(HapticsKey, 1) != 0;
            ReducedMotion = _store.GetInt(ReducedKey, 0) != 0;
        }

        /// <summary>Спільні налаштування гри; створюються на PlayerPrefs при першому зверненні.</summary>
        public static GameSettings Shared
        {
            get
            {
                if (_shared == null)
                {
                    _shared = new GameSettings(new PlayerPrefsSettingsStore());
                }

                return _shared;
            }
        }

        /// <summary>Підставляє спільні налаштування (тести); null скидає до PlayerPrefs.</summary>
        public static void Install(GameSettings settings)
        {
            _shared = settings;
        }

        public bool MusicOn { get; private set; }

        public bool SoundsOn { get; private set; }

        /// <summary>Прапор вібрації; сервіс вібрації (Q-05i) читає його.</summary>
        public bool HapticsEnabled { get; private set; }

        public bool ReducedMotion { get; private set; }

        public void SetMusic(bool on)
        {
            MusicOn = on;
            Write(MusicKey, on);
        }

        public void SetSounds(bool on)
        {
            SoundsOn = on;
            Write(SoundsKey, on);
        }

        public void SetHaptics(bool on)
        {
            HapticsEnabled = on;
            Write(HapticsKey, on);
        }

        public void SetReducedMotion(bool on)
        {
            ReducedMotion = on;
            Write(ReducedKey, on);
        }

        /// <summary>Підключає налаштування до виходу звуку й руху: музика й «Ефекти» - окремі шини, «менше анімацій» - UiMotion.</summary>
        public void ApplyTo(SfxRouter router)
        {
            if (router != null)
            {
                router.MusicVolume = MusicOn ? 1f : 0f;
                router.EffectsVolume = SoundsOn ? 1f : 0f;
            }

            UiMotion.Reduced = ReducedMotion;
        }

        private void Write(string key, bool on)
        {
            _store.SetInt(key, on ? 1 : 0);
            _store.Flush();
        }
    }
}
