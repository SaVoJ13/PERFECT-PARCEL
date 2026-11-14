using System;
using System.Collections.Generic;
using UnityEngine;

namespace KebabStreet.Game
{
    /// <summary>
    /// Вихід тимчасових звуків (S1-13) на AudioSource. Звук без файлу чи без джерела — тиша, без винятків.
    /// Клипи завантажуються з <c>Resources/Audio</c> один раз і кешуються (у тому числі відсутність).
    /// </summary>
    public sealed class UnitySfxSink : ISfxSink
    {
        /// <summary>Гучність за замовчуванням (повзунка немає); піки у файлах і так помірні.</summary>
        public const float DefaultVolume = 0.8f;

        private readonly AudioSource _source;
        private readonly Func<string, AudioClip> _load;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

        public UnitySfxSink(AudioSource source, Func<string, AudioClip> load = null)
        {
            _source = source;
            _load = load ?? DefaultLoad;
        }

        /// <summary>Завантажує клип із Resources/Audio; немає файлу — null.</summary>
        public static AudioClip DefaultLoad(string name)
        {
            return string.IsNullOrEmpty(name) ? null : Resources.Load<AudioClip>(SfxCatalog.AudioFolder + name);
        }

        public void Play(SfxCue cue)
        {
            string name = SfxCatalog.NameOf(cue);
            if (name == null || _source == null)
            {
                return;
            }

            AudioClip clip;
            if (!_clips.TryGetValue(name, out clip))
            {
                clip = _load(name);
                _clips[name] = clip;
            }

            if (clip != null)
            {
                _source.PlayOneShot(clip, DefaultVolume);
            }
        }

        /// <summary>Створює вихід звуку на окремому об'єкті, що переживає зміну сцен; повторний виклик нічого не робить.</summary>
        public static void EnsureInstalled()
        {
            if (!Application.isPlaying || _installed)
            {
                return;
            }

            _installed = true;
            var holder = new GameObject("Sfx");
            UnityEngine.Object.DontDestroyOnLoad(holder);
            AttachListener(holder);
            AudioSource source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            SfxHub.Install(new UnitySfxSink(source));
        }

        /// <summary>
        /// S1-23 (BUG-054): у сценах немає камери зі слухачем, тож без <see cref="AudioListener"/> звуку не чути.
        /// Додає слухача на об'єкт, якщо ні на ньому, ні будь-де в сцені його ще немає; повторний виклик не дублює.
        /// Повертає кількість слухачів на об'єкті (0 або 1).
        /// </summary>
        public static int AttachListener(GameObject holder)
        {
            if (holder == null)
            {
                return 0;
            }

            if (holder.GetComponent<AudioListener>() == null && UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null)
            {
                holder.AddComponent<AudioListener>();
            }

            return holder.GetComponents<AudioListener>().Length;
        }

        private static bool _installed;
    }
}
