using System;
using System.Collections.Generic;
using KebabStreet.Services;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Програмовані покупки для тестів. Живе лише в тестовій збірці (Editor), у плеєр не потрапляє.</summary>
    public sealed class FakePurchaseService : IPurchaseService
    {
        public bool IsAvailable { get; set; } = true;

        /// <summary>Каталог: ID продукту → відомості.</summary>
        public Dictionary<string, ProductInfo> Products { get; } = new Dictionary<string, ProductInfo>();

        public PurchaseResult NextPurchaseResult { get; set; } = PurchaseResult.Success;

        public RestoreResult NextRestoreResult { get; set; } = RestoreResult.Completed;

        /// <summary>Журнал викликів: «purchase:id», «restore».</summary>
        public List<string> Calls { get; } = new List<string>();

        public bool TryGetProduct(string productId, out ProductInfo product)
        {
            product = null;
            return IsAvailable && productId != null && Products.TryGetValue(productId, out product);
        }

        public void Purchase(string productId, Action<PurchaseResult> onResult)
        {
            Calls.Add("purchase:" + productId);
            var known = productId != null && Products.ContainsKey(productId);
            onResult?.Invoke(IsAvailable && known ? NextPurchaseResult : PurchaseResult.Unavailable);
        }

        public void Restore(Action<RestoreResult> onResult)
        {
            Calls.Add("restore");
            onResult?.Invoke(IsAvailable ? NextRestoreResult : RestoreResult.Unavailable);
        }
    }
}
