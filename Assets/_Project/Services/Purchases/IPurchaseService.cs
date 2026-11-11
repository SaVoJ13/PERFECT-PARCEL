using System;

namespace KebabStreet.Services
{
    /// <summary>
    /// Контракт покупок без прив'язки до SDK. Асинхронність — колбеками.
    /// Правило UI: кнопку покупки показувати лише коли <see cref="IsAvailable"/> і <see cref="TryGetProduct"/> повертає true.
    /// Продукти й ціни — з iap_catalog.json; тут лише ID.
    /// </summary>
    public interface IPurchaseService
    {
        /// <summary>Чи існує справжній сервіс покупок і він готовий (false для заглушки Null).</summary>
        bool IsAvailable { get; }

        /// <summary>Каталог за ID продукту; false, якщо продукту немає або сервіс недоступний.</summary>
        bool TryGetProduct(string productId, out ProductInfo product);

        /// <summary>Купує продукт. <paramref name="onResult"/> викликається рівно один раз на запит.</summary>
        void Purchase(string productId, Action<PurchaseResult> onResult);

        /// <summary>Відновлює разові покупки (після перевстановлення).</summary>
        void Restore(Action<RestoreResult> onResult);
    }
}
