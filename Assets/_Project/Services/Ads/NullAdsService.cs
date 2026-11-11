using System;

namespace KebabStreet.Services
{
    /// <summary>Реліз до Етапу 3: нічого не показує й ніколи не нагороджує.</summary>
    public sealed class NullAdsService : IAdsService
    {
        public bool IsAvailable => false;

        public bool IsRewardedReady => false;

        public bool IsInterstitialReady => false;

        public void ShowRewarded(string placement, Action<AdResult> onResult)
        {
            onResult?.Invoke(AdResult.NotReady);
        }

        public void ShowInterstitial(string placement, Action<AdResult> onResult)
        {
            onResult?.Invoke(AdResult.NotReady);
        }
    }
}
