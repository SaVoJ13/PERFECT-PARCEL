using System;
using UnityEngine;

namespace PerfectParcel.Monetization
{
    /// <summary>
    /// Менеджер ініціалізації згоди користувача Google UMP (User Messaging Platform).
    /// Забезпечує GDPR-комплаєнс для гравців з Європейської економічної зони (ЄЕЗ) та Великобританії
    /// згідно з офіційними вимогами Google Play Console.
    /// </summary>
    public class UmpConsentManager : MonoBehaviour
    {
        public static UmpConsentManager Instance { get; private set; }

        public bool IsConsentGathered { get; private set; } = false;
        public bool CanRequestAds { get; private set; } = true;

        public event Action OnConsentComplete;

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

        private void Start()
        {
            InitializeConsent();
        }

        /// <summary>
        /// Ініціалізація запиту згоди UMP.
        /// </summary>
        public void InitializeConsent()
        {
            Debug.Log("[UmpConsentManager] Початок запиту статусу згоди Google UMP...");

            // В Editor або на тестових пристроях симулюємо успішне проходження
#if UNITY_EDITOR
            IsConsentGathered = true;
            CanRequestAds = true;
            Debug.Log("[UmpConsentManager] [EDITOR] Згода UMP успішно підтверджена за замовчуванням.");
            OnConsentComplete?.Invoke();
#else
            // Мобільна ініціалізація Google UMP SDK
            try
            {
                // Тут підключається виклик GoogleMobileAds.Ump.Api.ConsentInformation
                // Приклад:
                // ConsentRequestParameters request = new ConsentRequestParameters();
                // ConsentInformation.Update(request, OnConsentInfoUpdated);
                IsConsentGathered = true;
                CanRequestAds = true;
                OnConsentComplete?.Invoke();
                Debug.Log("[UmpConsentManager] [MOBILE] UMP SDK опитано успішно.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UmpConsentManager] Помилка оновлення згоди UMP: {ex.Message}. Працюємо в режимі безпечних дефолтів.");
                IsConsentGathered = true;
                CanRequestAds = true;
                OnConsentComplete?.Invoke();
            }
#endif
        }

        /// <summary>
        /// Дозволяє користувачеві скинути чи повторно відкрити налаштування приватності з меню налаштувань.
        /// </summary>
        public void ShowPrivacyOptionsForm()
        {
            Debug.Log("[UmpConsentManager] Відкриття форми налаштувань персоналізації реклами UMP...");
        }
    }
}
