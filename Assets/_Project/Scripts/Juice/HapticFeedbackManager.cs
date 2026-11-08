using UnityEngine;
using PerfectParcel.Core;
using PerfectParcel.Storage;

namespace PerfectParcel.Juice
{
    /// <summary>
    /// Менеджер тактильного відгуку (Haptics & Vibration).
    /// Забезпечує вібрацію пристрою на помилках та при отриманні нагород
    /// із суворим дотриманням налаштування увімкнення/вимкнення в Settings.
    /// </summary>
    public class HapticFeedbackManager : MonoBehaviour
    {
        public static HapticFeedbackManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LivesChangedEvent>(OnLivesChanged);
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LivesChangedEvent>(OnLivesChanged);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        public bool IsHapticsEnabled()
        {
            return SaveManager.Instance?.CurrentData?.settings?.hapticsEnabled ?? true;
        }

        public void TriggerErrorVibration()
        {
            if (!IsHapticsEnabled()) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#else
            Debug.Log("[HapticFeedbackManager] [SIMULATION] Вібрація помилки (Error Haptic Pulse).");
#endif
        }

        public void TriggerSuccessVibration()
        {
            if (!IsHapticsEnabled()) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#else
            Debug.Log("[HapticFeedbackManager] [SIMULATION] Тактильний імпульс успіху (Success Haptic).");
#endif
        }

        private void OnLivesChanged(LivesChangedEvent e)
        {
            TriggerErrorVibration();
        }

        private void OnGameStateChanged(GameStateChangedEvent e)
        {
            if (e.NewState == GameState.LevelComplete)
            {
                TriggerSuccessVibration();
            }
            else if (e.NewState == GameState.GameOver)
            {
                TriggerErrorVibration();
            }
        }
    }
}
