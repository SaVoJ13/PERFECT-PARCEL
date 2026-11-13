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
            AudioSource source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            SfxHub.Install(new UnitySfxSink(source));
        }

        private static bool _installed;
    }
}
