using System;
using NUnit.Framework;

namespace TiltDrop.Core.Tests
{
    public class ServicesTests
    {
        sealed class FakeAds : IChildSafeAds
        {
            public string Name; public bool Inited;
            public void Initialize(PrivacyDecision d, Action<ConsentResult> done) { Inited = true; done(new ConsentResult { CanRequestAds = true }); }
            public bool IsRewardedReady => true;
            public bool IsInterstitialReady => true;
            public void ShowRewarded(string p, Action<bool> done) => done(true);
            public void ShowInterstitial(string p, Action done) => done();
            public bool PrivacyOptionsRequired => false;
            public void ShowPrivacyOptions(Action done) => done();
        }

        [Test]
        public void RouterPicksAdultOrChildSafePath()
        {
            var adult = new FakeAds { Name = "max" };
            var child = new FakeAds { Name = "admob" };
            var r = new AdsRouter(adult, child);
            r.Initialize(PrivacyRules.Decide(AgeBand.Under16, false, true, true, false), _ => { });
            Assert.IsTrue(child.Inited); Assert.IsFalse(adult.Inited);
            Assert.IsTrue(r.IsRewardedReady);

            var adult2 = new FakeAds();
            var r2 = new AdsRouter(adult2, null);
            r2.Initialize(PrivacyRules.Decide(AgeBand.Adult, false, true, true, false), _ => { });
            Assert.IsTrue(adult2.Inited);
        }

        [Test]
        public void UnderSixteenWithoutChildSafeSdkGetsNoAds()
        {
            var adult = new FakeAds();
            var r = new AdsRouter(adult, null);
            bool? rewarded = null;
            r.Initialize(PrivacyRules.Decide(AgeBand.Under13, true, true, true, false), _ => { });
            Assert.IsFalse(adult.Inited, "MAX is never initialized for under-16 users");
            Assert.IsFalse(r.IsRewardedReady);
            r.ShowRewarded("x", ok => rewarded = ok);
            Assert.AreEqual(false, rewarded);
        }

        [Test]
        public void RegistryFallsBackToStub()
        {
            ServiceRegistry.Clear();
            Assert.AreEqual("stub", ServiceRegistry.Resolve<IAnalytics>(() => null) == null ? "stub" : "real");
            Assert.IsFalse(ServiceRegistry.IsRegistered<IAdsService>());
            ServiceRegistry.Register<IAdsService>(() => new FakeAds { Name = "real" });
            Assert.AreEqual("real", ((FakeAds)ServiceRegistry.Resolve<IAdsService>(() => null)).Name);
            ServiceRegistry.Clear();
        }
    }
}
