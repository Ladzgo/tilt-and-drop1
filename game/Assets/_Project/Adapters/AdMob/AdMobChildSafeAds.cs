// Under-16 ad path (COMPLIANCE §3): Google Mobile Ads Unity plugin used directly with
// under-age-of-consent tags, G-rated, non-personalized. Compiled only with TD_ADMOB.
// Written against the Google Mobile Ads Unity plugin v9 API; re-check after upgrades.
using System;
using GoogleMobileAds.Api;
using TiltDrop.Core;
using UnityEngine;

namespace TiltDrop.Adapters
{
    public sealed class AdMobChildSafeAds : IChildSafeAds
    {
#if UNITY_IOS
        const string RewardedId = "YOUR_IOS_ADMOB_REWARDED_ID";
        const string InterstitialId = "YOUR_IOS_ADMOB_INTERSTITIAL_ID";
#else
        const string RewardedId = "YOUR_ANDROID_ADMOB_REWARDED_ID";
        const string InterstitialId = "YOUR_ANDROID_ADMOB_INTERSTITIAL_ID";
#endif
        RewardedAd rewarded;
        InterstitialAd interstitial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => ServiceRegistry.Register<IChildSafeAds>(() => new AdMobChildSafeAds());

        public void Initialize(PrivacyDecision d, Action<ConsentResult> done)
        {
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                TagForUnderAgeOfConsent = TagForUnderAgeOfConsent.True,
                TagForChildDirectedTreatment = d.ChildDirected ? TagForChildDirectedTreatment.True : TagForChildDirectedTreatment.Unspecified,
                MaxAdContentRating = MaxAdContentRating.G,
            });
            MobileAds.Initialize(_ =>
            {
                LoadRewarded();
                LoadInterstitial();
                done(new ConsentResult { CanRequestAds = true, Personalized = false, Analytics = false });
            });
        }

        static AdRequest Request()
        {
            var r = new AdRequest();
            r.Extras.Add("npa", "1"); // non-personalized
            return r;
        }

        void LoadRewarded() => RewardedAd.Load(RewardedId, Request(), (ad, err) => { if (err == null) rewarded = ad; });
        void LoadInterstitial() => InterstitialAd.Load(InterstitialId, Request(), (ad, err) => { if (err == null) interstitial = ad; });

        public bool IsRewardedReady => rewarded != null && rewarded.CanShowAd();
        public bool IsInterstitialReady => interstitial != null && interstitial.CanShowAd();

        public void ShowRewarded(string placement, Action<bool> done)
        {
            if (!IsRewardedReady) { done(false); return; }
            bool earned = false;
            var ad = rewarded;
            rewarded = null;
            ad.OnAdFullScreenContentClosed += () => { ad.Destroy(); LoadRewarded(); done(earned); };
            ad.OnAdFullScreenContentFailed += _ => { ad.Destroy(); LoadRewarded(); done(false); };
            ad.Show(_ => earned = true);
        }

        public void ShowInterstitial(string placement, Action done)
        {
            if (!IsInterstitialReady) { done(); return; }
            var ad = interstitial;
            interstitial = null;
            ad.OnAdFullScreenContentClosed += () => { ad.Destroy(); LoadInterstitial(); done(); };
            ad.OnAdFullScreenContentFailed += _ => { ad.Destroy(); LoadInterstitial(); done(); };
            ad.Show();
        }

        public bool PrivacyOptionsRequired => false;
        public void ShowPrivacyOptions(Action done) => done?.Invoke();
    }
}
