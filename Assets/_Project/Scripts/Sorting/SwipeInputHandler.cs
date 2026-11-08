using System;
using UnityEngine;
using PerfectParcel.Conveyor;
using PerfectParcel.Core;

namespace PerfectParcel.Sorting
{
    /// <summary>
    /// Обробник сенсорного та мишачого введення.
    /// Розпізнає швидкі свайпи вліво, вгору/прямо та вправо,
    /// а також підтримує прямі тапи по кнопках доступності.
    /// </summary>
    public class SwipeInputHandler : MonoBehaviour
    {
        public event Action<SortingDirection> OnSwipeDetected;
        public event Action<Vector2> OnTapDetected;

        private Vector2 _touchStartPos;
        private float _touchStartTime;
        private bool _isTrackingSwipe;

        private void Update()
        {
            HandleMouseInput();
            HandleTouchInput();
        }

        private void HandleTouchInput()
        {
            if (Input.touchCount == 0) return;

            var touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    StartTracking(touch.position);
                    break;
                case TouchPhase.Ended:
                    EndTracking(touch.position);
                    break;
                case TouchPhase.Canceled:
                    _isTrackingSwipe = false;
                    break;
            }
        }

        private void HandleMouseInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                StartTracking(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                EndTracking(Input.mousePosition);
            }
        }

        private void StartTracking(Vector2 screenPos)
        {
            _touchStartPos = screenPos;
            _touchStartTime = Time.time;
            _isTrackingSwipe = true;
        }

        private void EndTracking(Vector2 screenPos)
        {
            if (!_isTrackingSwipe) return;
            _isTrackingSwipe = false;

            var delta = screenPos - _touchStartPos;
            float distance = delta.magnitude;

            // Якщо відстань менша за поріг — це одиночний тап
            if (distance < GameConstants.MinSwipeDistance)
            {
                OnTapDetected?.Invoke(screenPos);
                return;
            }

            // Розрахунок кута свайпу
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            if (angle >= 45f && angle <= 135f)
            {
                // Свайп вгору / прямо
                Audio.AudioManager.Instance?.PlaySFX(Audio.SoundEffect.Swipe);
                OnSwipeDetected?.Invoke(SortingDirection.Straight);
            }
            else if (angle > 135f || angle <= -135f)
            {
                // Свайп ліворуч
                Audio.AudioManager.Instance?.PlaySFX(Audio.SoundEffect.Swipe);
                OnSwipeDetected?.Invoke(SortingDirection.Left);
            }
            else if (angle >= -45f && angle <= 45f)
            {
                // Свайп праворуч
                Audio.AudioManager.Instance?.PlaySFX(Audio.SoundEffect.Swipe);
                OnSwipeDetected?.Invoke(SortingDirection.Right);
            }
        }

        /// <summary>
        /// Допоміжний метод для виклику сортування з кнопок UI (доступність).
        /// </summary>
        public void TriggerDirectionFromUI(SortingDirection direction)
        {
            Audio.AudioManager.Instance?.PlaySFX(Audio.SoundEffect.Swipe);
            OnSwipeDetected?.Invoke(direction);
        }
    }
}
