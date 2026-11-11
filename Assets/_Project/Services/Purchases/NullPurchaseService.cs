using System;

namespace KebabStreet.Services
{
    /// <summary>Реліз до Етапу 3: покупок немає, нічого не нараховується.</summary>
    public sealed class NullPurchaseService : IPurchaseService
    {
        public bool IsAvailable => false;

        public bool TryGetProduct(string productId, out ProductInfo product)
        {
            product = null;
            return false;
        }

        public void Purchase(string productId, Action<PurchaseResult> onResult)
        {
            onResult?.Invoke(PurchaseResult.Unavailable);
        }

        public void Restore(Action<RestoreResult> onResult)
        {
            onResult?.Invoke(RestoreResult.Unavailable);
        }
    }
}
