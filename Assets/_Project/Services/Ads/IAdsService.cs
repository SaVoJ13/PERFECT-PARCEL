using System;

namespace KebabStreet.Services
{
    /// <summary>
    /// Контракт реклами без прив'язки до SDK. Асинхронність — колбеками.
    /// Правило UI: кнопку реклами показувати лише коли <see cref="IsAvailable"/> і відповідний <c>Is*Ready</c> істинні.
    /// Місця й частота реклами — у MONETIZATION_STRATEGY.md і ad_pacing.json, не тут.
    /// </summary>
    public interface IAdsService
    {
        /// <summary>Чи існує справжній рекламний сервіс (false для заглушки Null).</summary>
        bool IsAvailable { get; }

        bool IsRewardedReady { get; }

        bool IsInterstitialReady { get; }

        /// <summary>Показує rewarded. <paramref name="onResult"/> викликається рівно один раз; без готової реклами — з <see cref="AdResult.NotReady"/>.</summary>
        /// <param name="placement">Ключ місця показу (для аналітики й частоти), без персональних даних.</param>
        void ShowRewarded(string placement, Action<AdResult> onResult);

        /// <summary>Показує interstitial. Результат — <see cref="AdResult.Closed"/>, <see cref="AdResult.NotReady"/> або <see cref="AdResult.Failed"/>.</summary>
        void ShowInterstitial(string placement, Action<AdResult> onResult);
    }
}
