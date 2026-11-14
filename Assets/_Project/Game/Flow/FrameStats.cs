using System;
using System.Globalization;

namespace KebabStreet.Game
{
    /// <summary>
    /// Вимірювання кадрів (S1-22), логіка без Unity: приймає тривалість кадру, накопичує кількість, суму,
    /// найдовший кадр і кількість довгих кадрів; коли накопичений реальний час сягає інтервалу, віддає
    /// рядок звіту й скидає лічильники. На паузі гри кадри не враховуються (лог не спамить).
    /// На кадр не виділяє пам'яті; рядок створюється раз на інтервал.
    /// </summary>
    public sealed class FrameStats
    {
        /// <summary>Інтервал між рядками логу, с реального часу.</summary>
        public const float DefaultIntervalSec = 5f;

        /// <summary>Кадр довший за це, мс, вважається відчутним просіданням.</summary>
        public const float DefaultLongFrameMs = 50f;

        private readonly double _intervalSec;
        private readonly double _longFrameSec;
        private int _frames;
        private double _sumSec;
        private double _maxSec;
        private int _longFrames;

        public FrameStats(float intervalSec = DefaultIntervalSec, float longFrameMs = DefaultLongFrameMs)
        {
            if (intervalSec <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSec));
            }

            if (longFrameMs <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(longFrameMs));
            }

            _intervalSec = intervalSec;
            _longFrameSec = longFrameMs / 1000.0;
        }

        public int Frames => _frames;

        public int LongFrames => _longFrames;

        public double MaxFrameMs => _maxSec * 1000.0;

        public double AverageFps => _sumSec > 0.0 ? _frames / _sumSec : 0.0;

        /// <summary>
        /// Враховує кадр. true — інтервал накопичено, можна взяти звіт через <see cref="TakeReport"/>.
        /// Кадри на паузі та від'ємні/нульові тривалості ігноруються.
        /// </summary>
        public bool Frame(float deltaTimeSec, bool paused)
        {
            if (paused || deltaTimeSec <= 0f)
            {
                return false;
            }

            _frames++;
            _sumSec += deltaTimeSec;
            if (deltaTimeSec > _maxSec)
            {
                _maxSec = deltaTimeSec;
            }

            if (deltaTimeSec > _longFrameSec)
            {
                _longFrames++;
            }

            return _sumSec >= _intervalSec;
        }

        /// <summary>Рядок звіту без префікса [KS] і скидання лічильників. Порожній, якщо кадрів не було.</summary>
        public string TakeReport()
        {
            if (_frames == 0)
            {
                return string.Empty;
            }

            string line = string.Format(
                CultureInfo.InvariantCulture,
                "frames={0} avgFps={1:F1} maxMs={2:F1} over{3:F0}ms={4}",
                _frames,
                AverageFps,
                MaxFrameMs,
                _longFrameSec * 1000.0,
                _longFrames);
            Reset();
            return line;
        }

        public void Reset()
        {
            _frames = 0;
            _sumSec = 0.0;
            _maxSec = 0.0;
            _longFrames = 0;
        }
    }
}
