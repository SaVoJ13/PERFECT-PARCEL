using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KebabStreet.Game;
using KebabStreet.Services;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Контракти реклами, покупок і аналітики та заглушки (E0-09).</summary>
    public sealed class PlatformServicesTests
    {
        [TearDown]
        public void ResetContext()
        {
            GameContext.ResetForTests();
        }

        // ---- Null: безпечні, ніколи не нагороджують ----

        [Test]
        public void NullAds_NeverReady_AndAlwaysNotReady_ExactlyOneCallback()
        {
            IAdsService ads = new NullAdsService();
            Assert.IsFalse(ads.IsAvailable);
            Assert.IsFalse(ads.IsRewardedReady);
            Assert.IsFalse(ads.IsInterstitialReady);

            var results = new List<AdResult>();
            ads.ShowRewarded("p", results.Add);
            ads.ShowInterstitial("p", results.Add);
            CollectionAssert.AreEqual(new[] { AdResult.NotReady, AdResult.NotReady }, results);
            Assert.DoesNotThrow(() => ads.ShowRewarded("p", null));
            Assert.DoesNotThrow(() => ads.ShowInterstitial(null, null));
        }

        [Test]
        public void NullPurchases_Unavailable_NoProducts_NeverSuccess()
        {
            IPurchaseService shop = new NullPurchaseService();
            Assert.IsFalse(shop.IsAvailable);
            Assert.IsFalse(shop.TryGetProduct("any", out var product));
            Assert.IsNull(product);

            PurchaseResult? purchase = null;
            RestoreResult? restore = null;
            shop.Purchase("any", r => purchase = r);
            shop.Restore(r => restore = r);
            Assert.AreEqual(PurchaseResult.Unavailable, purchase);
            Assert.AreEqual(RestoreResult.Unavailable, restore);
            Assert.DoesNotThrow(() => shop.Purchase(null, null));
            Assert.DoesNotThrow(() => shop.Restore(null));
        }

        [Test]
        public void NullAnalytics_IsUnavailable_AndIgnoresInput()
        {
            IAnalyticsService analytics = new NullAnalyticsService();
            Assert.IsFalse(analytics.IsAvailable);
            Assert.DoesNotThrow(() => analytics.LogEvent("e", null));
            Assert.DoesNotThrow(() => analytics.LogEvent(null, new Dictionary<string, string> { { "k", "v" } }));
            Assert.DoesNotThrow(() => analytics.SetUserProperty("p", null));
        }

        // ---- Fake: задані результати й журнал ----

        [Test]
        public void FakeAds_ReturnsProgrammedResults_AndLogsCalls()
        {
            var ads = new FakeAdsService { NextRewardedResult = AdResult.Closed, NextInterstitialResult = AdResult.Failed };
            var results = new List<AdResult>();
            ads.ShowRewarded("double_coins", results.Add);
            ads.ShowInterstitial("between_levels", results.Add);
            CollectionAssert.AreEqual(new[] { AdResult.Closed, AdResult.Failed }, results);
            CollectionAssert.AreEqual(new[] { "rewarded:double_coins", "interstitial:between_levels" }, ads.Calls);
        }

        [Test]
        public void FakeAds_NotReady_ReturnsNotReady_NeverRewarded()
        {
            var ads = new FakeAdsService { IsRewardedReady = false, IsInterstitialReady = false };
            var results = new List<AdResult>();
            ads.ShowRewarded("p", results.Add);
            ads.ShowInterstitial("p", results.Add);
            CollectionAssert.AreEqual(new[] { AdResult.NotReady, AdResult.NotReady }, results);
        }

        [Test]
        public void FakePurchases_ProgrammedResults_CatalogAndLog()
        {
            var shop = new FakePurchaseService();
            shop.Products["remove_ads"] = new ProductInfo("remove_ads", "$4.99");

            Assert.IsTrue(shop.TryGetProduct("remove_ads", out var info));
            Assert.AreEqual("$4.99", info.LocalizedPrice);
            Assert.IsFalse(shop.TryGetProduct("missing", out _));

            PurchaseResult? purchase = null;
            shop.NextPurchaseResult = PurchaseResult.Pending;
            shop.Purchase("remove_ads", r => purchase = r);
            Assert.AreEqual(PurchaseResult.Pending, purchase);

            shop.Purchase("missing", r => purchase = r);
            Assert.AreEqual(PurchaseResult.Unavailable, purchase);

            RestoreResult? restore = null;
            shop.Restore(r => restore = r);
            Assert.AreEqual(RestoreResult.Completed, restore);

            CollectionAssert.AreEqual(new[] { "purchase:remove_ads", "purchase:missing", "restore" }, shop.Calls);
        }

        [Test]
        public void FakeAnalytics_RecordsEventsAndProperties()
        {
            var analytics = new FakeAnalyticsService();
            analytics.LogEvent("level_start", new Dictionary<string, string> { { "level", "1" } });
            analytics.SetUserProperty("tier", "a");
            Assert.AreEqual(1, analytics.Events.Count);
            Assert.AreEqual("level_start", analytics.Events[0].Name);
            Assert.AreEqual("1", analytics.Events[0].Parameters["level"]);
            Assert.AreEqual("a", analytics.UserProperties["tier"]);
        }

        // ---- GameContext ----

        [Test]
        public void GameContext_DefaultsToNullServices()
        {
            var context = new GameContext(new InMemorySaveStorage(), new ManualClock(1), new PlaceholderLocalizer());
            Assert.IsInstanceOf<NullAdsService>(context.Ads);
            Assert.IsInstanceOf<NullPurchaseService>(context.Purchases);
            Assert.IsInstanceOf<NullAnalyticsService>(context.Analytics);
            Assert.IsFalse(context.Ads.IsAvailable);
            Assert.IsFalse(context.Purchases.IsAvailable);
            Assert.IsFalse(context.Analytics.IsAvailable);
        }

        [Test]
        public void GameContext_AcceptsInjectedServices_AndFallsBackToNullForNull()
        {
            var ads = new FakeAdsService();
            var context = new GameContext(
                new InMemorySaveStorage(), new ManualClock(1), new PlaceholderLocalizer(), ads, null, null);
            Assert.AreSame(ads, context.Ads);
            Assert.IsInstanceOf<NullPurchaseService>(context.Purchases);
            Assert.IsInstanceOf<NullAnalyticsService>(context.Analytics);
        }

        // ---- Fake не в релізній збірці ----

        private static readonly string[] ShippedAssemblies =
        {
            "KebabStreet.Core", "KebabStreet.Services", "KebabStreet.Game", "KebabStreet.Platform",
        };

        [Test]
        public void ShippedAssemblies_ContainNoFakeServices()
        {
            var serviceInterfaces = new[] { typeof(IAdsService), typeof(IPurchaseService), typeof(IAnalyticsService) };
            var checkedAssemblies = 0;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!ShippedAssemblies.Contains(assembly.GetName().Name))
                {
                    continue;
                }

                checkedAssemblies++;
                foreach (var type in assembly.GetTypes())
                {
                    if (serviceInterfaces.Any(i => i.IsAssignableFrom(type)) && !type.IsInterface)
                    {
                        StringAssert.DoesNotStartWith("Fake", type.Name, "Fake-заглушка в релізній збірці: " + type.FullName);
                    }
                }
            }

            Assert.GreaterOrEqual(checkedAssemblies, 3, "Очікувано щонайменше Core, Services і Game.");
        }

        [Test]
        public void FakeServices_LiveOnlyInEditorOnlyTestAssembly()
        {
            var testAssembly = typeof(FakeAdsService).Assembly;
            Assert.AreEqual("KebabStreet.Tests.EditMode", testAssembly.GetName().Name);
            Assert.AreSame(testAssembly, typeof(FakePurchaseService).Assembly);
            Assert.AreSame(testAssembly, typeof(FakeAnalyticsService).Assembly);

            // Збірка тестів має includePlatforms: ["Editor"] — у плеєр не збирається.
            var asmdef = System.IO.File.ReadAllText("Assets/_Project/Tests/EditMode/KebabStreet.Tests.EditMode.asmdef");
            StringAssert.Contains("\"includePlatforms\": [\"Editor\"]", asmdef);
            StringAssert.Contains("UNITY_INCLUDE_TESTS", asmdef);
        }

        [Test]
        public void NullServices_LiveInServicesAssembly()
        {
            var services = typeof(IAdsService).Assembly;
            Assert.AreSame(services, typeof(NullAdsService).Assembly);
            Assert.AreSame(services, typeof(NullPurchaseService).Assembly);
            Assert.AreSame(services, typeof(NullAnalyticsService).Assembly);
        }
    }
}
