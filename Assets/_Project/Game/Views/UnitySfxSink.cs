using System;
using System.Collections.Generic;
using UnityEngine;

namespace KebabStreet.Game
{
    /// <summary>
    /// Вихід звуків на AudioSource (S1-13, Q-05g). Файлові звуки — з <c>Resources/Audio</c>, синтезовані — з
    /// <see cref="SfxSynth"/> через <c>AudioClip.Create</c>; усе кешується. Одноразові звуки йдуть через невеликий пул
    /// джерел (кожне зі своєю висотою), петлі шипіння й музики — на власних джерелах. Звук без файлу чи без
    /// джерела — тиша, без винятків. Без алокацій під час відтворення.
    /// </summary>
    public sealed class UnitySfxSink : ISfxSink
    {
        /// <summary>Гучність за замовчуванням (до шини «Ефекти»); піки у файлах і так помірні.</summary>
        public const float DefaultVolume = 0.8f;

        /// <summary>Розмір пулу джерел одноразових звуків.</summary>
        public const int PoolSize = 6;

        private readonly AudioSource _source;
        private readonly Func<string, AudioClip> _load;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<SfxCue, AudioClip> _synth = new Dictionary<SfxCue, AudioClip>();
        private readonly AudioSource[] _pool;
        private int _next;
        private AudioSource _grill;
        private AudioSource _music;
        private float _effects = 1f;
        private float _musicVolume = 1f;

        public UnitySfxSink(AudioSource source, Func<string, AudioClip> load = null)
        {
            _source = source;
            _load = load ?? DefaultLoad;
            if (source != null)
            {
                _pool = new AudioSource[PoolSize];
                _pool[0] = source;
                for (int i = 1; i < PoolSize; i++)
                {
                    _pool[i] = CloneSource(source);
                }

                _grill = CloneSource(source);
                _grill.loop = true;
                _music = CloneSource(source);
                _music.loop = true;
            }
        }

        /// <summary>Завантажує клип із Resources/Audio; немає файлу — null.</summary>
        public static AudioClip DefaultLoad(string name)
        {
            return string.IsNullOrEmpty(name) ? null : Resources.Load<AudioClip>(SfxCatalog.AudioFolder + name);
        }

        /// <summary>Клип синтезованого звуку з кешу (створює при першому зверненні); null — звук не синтезований.</summary>
        public AudioClip SynthClip(SfxCue cue)
        {
            AudioClip clip;
            if (_synth.TryGetValue(cue, out clip))
            {
                return clip;
            }

            float[] data = SfxSynth.Generate(cue);
            clip = data.Length == 0 ? null : MakeClip("sfx_synth_" + cue, data);
            _synth[cue] = clip;
            return clip;
        }

        /// <summary>Скільки різних синтезованих клипів уже створено (для тесту кешу).</summary>
        public int SynthClipCount => _synth.Count;

        public void Play(SfxCue cue, float pitch)
        {
            if (_source == null)
            {
                return;
            }

            AudioClip clip = ClipOf(cue);
            if (clip == null)
            {
                return;
            }

            AudioSource src = _pool[_next];
            _next = (_next + 1) % PoolSize;
            src.pitch = pitch;
            src.clip = clip;
            src.volume = DefaultVolume * _effects;
            src.Play();
        }

        public void SetLoop(SfxLoop loop, bool on)
        {
            AudioSource src = loop == SfxLoop.Grill ? _grill : _music;
            if (src == null)
            {
                return;
            }

            if (on)
            {
                if (src.clip == null)
                {
                    float[] data = SfxSynth.GenerateLoop(loop);
                    src.clip = MakeClip("sfx_loop_" + loop, data);
                }

                if (!src.isPlaying)
                {
                    src.Play();
                }
            }
            else if (src.isPlaying)
            {
                src.Pause();
            }
        }

        public void SetVolumes(float effects, float music)
        {
            _effects = effects;
            _musicVolume = music;
            if (_grill != null)
            {
                _grill.volume = effects;
            }

            if (_music != null)
            {
                _music.volume = music;
            }
        }

        /// <summary>Поточна гучність музики на виході (для тестів).</summary>
        public float MusicVolumeNow => _musicVolume;

        private AudioClip ClipOf(SfxCue cue)
        {
            if (SfxCatalog.IsSynth(cue))
            {
                return SynthClip(cue);
            }

            string name = SfxCatalog.NameOf(cue);
            if (name == null)
            {
                return null;
            }

            AudioClip clip;
            if (!_clips.TryGetValue(name, out clip))
            {
                clip = _load(name);
                _clips[name] = clip;
            }

            return clip;
        }

        private static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SfxSynth.SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioSource CloneSource(AudioSource template)
        {
            AudioSource src = template.gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            return src;
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
