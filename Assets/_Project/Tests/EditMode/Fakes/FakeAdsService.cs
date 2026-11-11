using System;
using System.Collections.Generic;
using KebabStreet.Services;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Програмована реклама для тестів. Живе лише в тестовій збірці (Editor), у плеєр не потрапляє.</summary>
    public sealed class FakeAdsService : IAdsService
    {
        public bool IsAvailable { get; set; } = true;

        public bool IsRewardedReady { get; set; } = true;

        public bool IsInterstitialReady { get; set; } = true;

        /// <summary>Що повернути наступним показом rewarded, коли реклама готова.</summary>
        public AdResult NextRewardedResult { get; set; } = AdResult.Rewarded;

        public AdResult NextInterstitialResult { get; set; } = AdResult.Closed;

        /// <summary>Журнал викликів: «rewarded:місце», «interstitial:місце».</summary>
        public List<string> Calls { get; } = new List<string>();

        public void ShowRewarded(string placement, Action<AdResult> onResult)
        {
            Calls.Add("rewarded:" + placement);
            onResult?.Invoke(IsAvailable && IsRewardedReady ? NextRewardedResult : AdResult.NotReady);
        }

        public void ShowInterstitial(string placement, Action<AdResult> onResult)
        {
            Calls.Add("interstitial:" + placement);
            onResult?.Invoke(IsAvailable && IsInterstitialReady ? NextInterstitialResult : AdResult.NotReady);
        }
    }
}
