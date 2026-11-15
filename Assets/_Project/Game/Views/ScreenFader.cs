using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Математика переходів між екранами (Q-05e, QB-ALL-04): вихідний екран гасне й зсувається, вхідний проявляється.
    /// Лише opacity і translate (розмітка не перераховується, QB-ALL-16). «Менше анімацій» - перехід миттєвий.
    /// </summary>
    public static class ScreenTransition
    {
        public const float InSec = 0.25f;
        public const float OutSec = 0.25f;
        public const float SlidePx = 24f;

        /// <summary>Прозорість затемнення при появі екрана: 1 -> 0.</summary>
        public static float InOverlay(float elapsedSec)
        {
            return UiMotion.Reduced || elapsedSec >= InSec ? 0f : 1f - Ease(elapsedSec / InSec);
        }

        /// <summary>Зсув вмісту при появі: від +24 px до 0.</summary>
        public static float InOffset(float elapsedSec)
        {
            return UiMotion.Reduced || elapsedSec >= InSec ? 0f : SlidePx * (1f - Ease(elapsedSec / InSec));
        }

        /// <summary>Прозорість затемнення при відході: 0 -> 1.</summary>
        public static float OutOverlay(float elapsedSec)
        {
            return UiMotion.Reduced || elapsedSec >= OutSec ? 1f : Ease(elapsedSec / OutSec);
        }

        /// <summary>Зсув вмісту при відході: від 0 до -24 px.</summary>
        public static float OutOffset(float elapsedSec)
        {
            return UiMotion.Reduced || elapsedSec >= OutSec ? -SlidePx : -SlidePx * Ease(elapsedSec / OutSec);
        }

        /// <summary>Тривалість відходу: у «менше анімацій» - 0.</summary>
        public static float OutDuration => UiMotion.Reduced ? 0f : OutSec;

        private static float Ease(float x)
        {
            float k = Mathf.Clamp01(x);
            return 1f - ((1f - k) * (1f - k));
        }
    }

    /// <summary>
    /// Затемнення й зсув між екранами (сцени). Накладка кольору фону лежить у корені документа і поглинає ввід,
    /// поки видима: під час переходу тапи ігноруються (QB-ALL-04). Час - сирий годинник <see cref="UiMotion.Clock"/>
    /// (не ігровий: пауза не має зупиняти перехід). Правил гри тут немає.
    /// </summary>
    public sealed class ScreenFader
    {
        public const string OverlayName = "screen-fade";
        private const long TickMs = 16;

        private readonly VisualElement _root;
        private readonly VisualElement _overlay;
        private readonly MotionTarget _overlayMotion;
        private readonly MotionTarget _contentMotion;
        private float _startedAt;
        private bool _entering;
        private bool _leaving;
        private bool _done;
        private Action _onLeft;
        private IVisualElementScheduledItem _ticker;

        public ScreenFader(VisualElement root, VisualElement content)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            _contentMotion = new MotionTarget(content);
            _overlay = new VisualElement { name = OverlayName };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.top = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = UiColors.NightPlum;
            _overlay.pickingMode = PickingMode.Position;
            _overlay.style.opacity = 0f;
            _overlayMotion = new MotionTarget(_overlay);
            _overlayMotion.Set(1f, 1f, 0f, 0f, 0f);
            root.Add(_overlay);
        }

        /// <summary>Іде перехід (вхід або відхід): ввід під накладкою не проходить.</summary>
        public bool Busy => _entering || _leaving;

        public bool Leaving => _leaving;

        /// <summary>Покадровий таймер підключено до панелі.</summary>
        public bool Attached => _ticker != null;

        /// <summary>Поточна прозорість накладки (для тестів).</summary>
        public float OverlayOpacity => _overlayMotion.Opacity;

        /// <summary>Поточний зсув вмісту, px (для тестів).</summary>
        public float ContentOffset => _contentMotion.TranslateY;

        /// <summary>Підключає покадровий таймер до панелі. У тестах без панелі не викликається: <see cref="Tick"/> кличуть самі.</summary>
        public void Attach()
        {
            _ticker = _root.schedule.Execute(Tick).Every(TickMs);
        }

        /// <summary>Поява екрана: накладка гасне, вміст виїжджає знизу.</summary>
        public void PlayIn()
        {
            _entering = true;
            _leaving = false;
            _done = false;
            _startedAt = UiMotion.Clock();
            Apply(1f, ScreenTransition.SlidePx);
            Tick();
        }

        /// <summary>
        /// Відхід: накладка темніє, вміст зсувається; по завершенні викликається <paramref name="onLeft"/> (завантаження сцени).
        /// Повторний виклик під час переходу ігнорується (false).
        /// </summary>
        public bool Leave(Action onLeft)
        {
            if (_leaving)
            {
                return false;
            }

            _leaving = true;
            _entering = false;
            _done = false;
            _onLeft = onLeft;
            _startedAt = UiMotion.Clock();
            Tick();
            return true;
        }

        /// <summary>Крок переходу за годинником; закінчивши, знімає блокування вводу (вхід) або викликає завантаження (відхід).</summary>
        public void Tick()
        {
            if (_done || (!_entering && !_leaving))
            {
                return;
            }

            float e = UiMotion.Clock() - _startedAt;
            if (_entering)
            {
                float overlay = ScreenTransition.InOverlay(e);
                Apply(overlay, ScreenTransition.InOffset(e));
                if (e >= ScreenTransition.InSec || UiMotion.Reduced)
                {
                    _entering = false;
                    _done = true;
                    _overlay.pickingMode = PickingMode.Ignore;
                    Apply(0f, 0f);
                }

                return;
            }

            Apply(ScreenTransition.OutOverlay(e), ScreenTransition.OutOffset(e));
            if (e >= ScreenTransition.OutDuration)
            {
                _done = true;
                Action done = _onLeft;
                _onLeft = null;
                done?.Invoke();
            }
        }

        /// <summary>
        /// Скасовує відхід (сцену не завантажено): накладка зникає, ввід знову проходить, екран стоїть як був
        /// (Q-05f, P3 з QA Q-05e).
        /// </summary>
        public void Cancel()
        {
            _leaving = false;
            _entering = false;
            _done = true;
            _onLeft = null;
            Apply(0f, 0f);
            _overlay.pickingMode = PickingMode.Ignore;
        }

        private void Apply(float overlayOpacity, float offsetY)
        {
            _overlayMotion.Set(1f, 1f, 0f, 0f, overlayOpacity);
            _overlay.pickingMode = overlayOpacity > 0f || _leaving ? PickingMode.Position : PickingMode.Ignore;
            _contentMotion.Set(1f, 1f, 0f, offsetY, 1f);
        }
    }
}
