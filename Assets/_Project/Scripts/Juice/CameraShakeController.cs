using System.Collections;
using UnityEngine;
using PerfectParcel.Core;

namespace PerfectParcel.Juice
{
    /// <summary>
    /// Контролер мікрострусу камери (Screen Shake).
    /// Забезпечує соковитий відгук (Juice) на помилки, втрату життів
    /// та падіння посилок за дедлайн (тривалість 0.15 с).
    /// </summary>
    public class CameraShakeController : MonoBehaviour
    {
        public static CameraShakeController Instance { get; private set; }

        private Vector3 _originalPos;
        private Coroutine _shakeCoroutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                _originalPos = transform.position;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LivesChangedEvent>(OnLivesChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LivesChangedEvent>(OnLivesChanged);
        }

        private void OnLivesChanged(LivesChangedEvent e)
        {
            // Якщо життя зменшилися — викликаємо мікрострус камери
            TriggerShake(0.15f, 25f);
        }

        public void TriggerShake(float duration = 0.15f, float magnitude = 20f)
        {
            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
                transform.position = _originalPos;
            }
            _shakeCoroutine = StartCoroutine(DoShake(duration, magnitude));
        }

        private IEnumerator DoShake(float duration, float magnitude)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float percent = elapsed / duration;
                float currentMagnitude = Mathf.Lerp(magnitude, 0f, percent);

                float offsetX = (Random.value * 2f - 1f) * currentMagnitude;
                float offsetY = (Random.value * 2f - 1f) * currentMagnitude;

                transform.position = _originalPos + new Vector3(offsetX, offsetY, 0f);
                yield return null;
            }

            transform.position = _originalPos;
            _shakeCoroutine = null;
        }
    }
}
