using System;
using System.Collections.Generic;
using UnityEngine;
using PerfectParcel.Core;
using PerfectParcel.Storage;

namespace PerfectParcel.Audio
{
    /// <summary>
    /// Головний менеджер аудіо-системи (AudioManager).
    /// Забезпечує відтворення фонової музики (BGM) та звукових ефектів (SFX)
    /// через пул AudioSource без додаткових алокацій пам'яті (Zero GC).
    /// Містить процедурний генератор синтезованих аудіокліпів високої якості,
    /// що гарантує роботу звуку на будь-якій платформі без сторонніх завантажень.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const int SampleRate = 44100;
        private const int SfxPoolSize = 6;

        [Header("Audio Sources")]
        private AudioSource _bgmSource;
        private AudioSource[] _sfxPool;
        private int _currentSfxIndex = 0;

        private Dictionary<SoundEffect, AudioClip> _sfxClips;
        private AudioClip _bgmClip;

        private float _musicVolume = 0.8f;
        private float _sfxVolume = 1.0f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeAudioSources();
                GenerateProceduralClips();
                LoadSettingsVolume();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            PlayBGM();
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

        private void InitializeAudioSources()
        {
            // BGM Source
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;

            // SFX Sources Pool
            _sfxPool = new AudioSource[SfxPoolSize];
            for (int i = 0; i < SfxPoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.loop = false;
                src.playOnAwake = false;
                _sfxPool[i] = src;
            }
        }

        private void LoadSettingsVolume()
        {
            if (SaveManager.Instance?.CurrentData?.settings != null)
            {
                _musicVolume = SaveManager.Instance.CurrentData.settings.musicVolume;
                _sfxVolume = SaveManager.Instance.CurrentData.settings.sfxVolume;
            }
            UpdateVolumes();
        }

        public void SetMusicVolume(float volume)
        {
            _musicVolume = Mathf.Clamp01(volume);
            if (_bgmSource != null) _bgmSource.volume = _musicVolume;
        }

        public void SetSfxVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
            if (_sfxPool != null)
            {
                for (int i = 0; i < _sfxPool.Length; i++)
                {
                    if (_sfxPool[i] != null) _sfxPool[i].volume = _sfxVolume;
                }
            }
        }

        private void UpdateVolumes()
        {
            SetMusicVolume(_musicVolume);
            SetSfxVolume(_sfxVolume);
        }

        public void PlayBGM()
        {
            if (_bgmSource == null || _bgmClip == null) return;
            _bgmSource.clip = _bgmClip;
            _bgmSource.volume = _musicVolume;
            _bgmSource.Play();
        }

        public void PlaySFX(SoundEffect effect)
        {
            if (_sfxClips == null || !_sfxClips.TryGetValue(effect, out var clip) || clip == null) return;

            var src = _sfxPool[_currentSfxIndex];
            _currentSfxIndex = (_currentSfxIndex + 1) % SfxPoolSize;

            src.volume = _sfxVolume;
            src.clip = clip;
            src.Play();
        }

        #region Event Subscriptions
        private int _lastCoinsInRun;

        private void OnScoreChanged(ScoreChangedEvent e)
        {
            // Звук монет — лише коли кількість зароблених монет справді зросла
            if (e.CurrentCoins > _lastCoinsInRun) PlaySFX(SoundEffect.CoinGain);
            _lastCoinsInRun = e.CurrentCoins;
        }

        private void OnLivesChanged(LivesChangedEvent e)
        {
            // Втрата життя (на старті рівня подія лише синхронізує HUD)
            if (e.Decreased) PlaySFX(SoundEffect.ErrorMistake);
        }

        private void OnParcelSorted(ParcelSortedEvent e)
        {
            // Звук помилки для неправильного сортування відтворює SortingValidator
            if (e.IsCorrect) PlaySFX(SoundEffect.SuccessSort);
        }

        private void OnGameStateChanged(GameStateChangedEvent e)
        {
            if (e.NewState == GameState.LevelComplete)
            {
                PlaySFX(SoundEffect.LevelComplete);
            }
            else if (e.NewState == GameState.GameOver)
            {
                PlaySFX(SoundEffect.GameOver);
            }
        }
        #endregion

        #region Procedural Audio Clip Synthesizer (Zero Assets Dependency)
        private void GenerateProceduralClips()
        {
            _sfxClips = new Dictionary<SoundEffect, AudioClip>
            {
                [SoundEffect.ButtonClick] = CreateToneClip("Click", 0.04f, 850f, 400f, 0.4f),
                [SoundEffect.Swipe] = CreateWhooshClip("Swipe", 0.12f, 0.35f),
                [SoundEffect.SuccessSort] = CreateBellChimeClip("SortSuccess", 0.22f, 1046.5f, 1567.9f, 0.6f),
                [SoundEffect.ErrorMistake] = CreateThudClip("SortError", 0.25f, 160f, 60f, 0.7f),
                [SoundEffect.FragileBreak] = CreateBreakClip("FragileBreak", 0.32f, 0.8f),
                [SoundEffect.CoinGain] = CreateBellChimeClip("CoinGain", 0.18f, 987.7f, 1318.5f, 0.5f),
                [SoundEffect.LevelComplete] = CreateFanfareClip("WinFanfare", 0.65f, 0.65f),
                [SoundEffect.GameOver] = CreateGameOverClip("GameOver", 0.55f, 0.6f)
            };

            _bgmClip = CreateLoFiBgmClip("WarehouseLoFiBGM", 4.0f, 0.35f);
            Debug.Log("[AudioManager] Усі 8 звукових ефектів та саундтрек BGM згенеровано в пам'яті (Zero GC).");
        }

        private static AudioClip CreateToneClip(string name, float duration, float startFreq, float endFreq, float volume)
        {
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, t);
                float env = 1f - t; // Linear decay
                samples[i] = Mathf.Sin(2f * Mathf.PI * currentFreq * (i / (float)SampleRate)) * env * volume;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateWhooshClip(string name, float duration, float volume)
        {
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float env = Mathf.Sin(t * Mathf.PI); // Bell curve
                float noise = (UnityEngine.Random.value * 2f - 1f);
                float freq = Mathf.Lerp(500f, 200f, t);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)SampleRate));
                samples[i] = (noise * 0.4f + tone * 0.6f) * env * volume;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateBellChimeClip(string name, float duration, float freq1, float freq2, float volume)
        {
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float env = Mathf.Pow(1f - t, 2.5f); // Fast decay
                float s1 = Mathf.Sin(2f * Mathf.PI * freq1 * (i / (float)SampleRate));
                float s2 = Mathf.Sin(2f * Mathf.PI * freq2 * (i / (float)SampleRate));
                samples[i] = (s1 * 0.6f + s2 * 0.4f) * env * volume;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateThudClip(string name, float duration, float startFreq, float endFreq, float volume)
        {
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float freq = Mathf.Lerp(startFreq, endFreq, t);
                float env = Mathf.Pow(1f - t, 2f);
                float tone = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * (i / (float)SampleRate))); // Square wave punch
                samples[i] = tone * env * volume * 0.6f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateBreakClip(string name, float duration, float volume)
        {
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float env = Mathf.Pow(1f - t, 3f);
                float noise = (UnityEngine.Random.value * 2f - 1f);
                float resonance = Mathf.Sin(2f * Mathf.PI * 2800f * (i / (float)SampleRate));
                samples[i] = (noise * 0.7f + resonance * 0.3f) * env * volume;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateFanfareClip(string name, float duration, float volume)
        {
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                int noteIdx = Mathf.Clamp((int)(t * notes.Length), 0, notes.Length - 1);
                float freq = notes[noteIdx];
                float subT = (t * notes.Length) - noteIdx;
                float env = 1f - subT * 0.6f;
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)SampleRate)) * env * volume;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateGameOverClip(string name, float duration, float volume)
        {
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];
            float[] notes = { 440f, 349.23f, 293.66f }; // A4, F4, D4

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                int noteIdx = Mathf.Clamp((int)(t * notes.Length), 0, notes.Length - 1);
                float freq = notes[noteIdx];
                float subT = (t * notes.Length) - noteIdx;
                float env = 1f - subT * 0.5f;
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)SampleRate)) * env * volume;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateLoFiBgmClip(string name, float duration, float volume)
        {
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];
            // 4-chords loop: Cmaj7, Am7, Dm7, G7
            float[] rootFreqs = { 130.81f, 110.00f, 146.83f, 98.00f }; // C3, A2, D3, G2

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                int chordIdx = Mathf.Clamp((int)(t * 4f), 0, 3);
                float root = rootFreqs[chordIdx];

                float bass = Mathf.Sin(2f * Mathf.PI * root * (i / (float)SampleRate));
                float fifth = Mathf.Sin(2f * Mathf.PI * (root * 1.5f) * (i / (float)SampleRate)) * 0.4f;
                float third = Mathf.Sin(2f * Mathf.PI * (root * 1.25f) * (i / (float)SampleRate)) * 0.3f;

                // Soft lo-fi groove pulse
                float beat = Mathf.Abs(Mathf.Sin(t * 8f * Mathf.PI));
                float groove = 0.7f + beat * 0.3f;

                samples[i] = (bass + fifth + third) * groove * volume * 0.4f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        #endregion
    }
}
