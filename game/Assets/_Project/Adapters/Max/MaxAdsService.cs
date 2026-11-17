// Compiled only when the AppLovin MAX Unity plugin is imported and TD_MAX is defined (docs/SETUP.md).
// Verified against the MAX Unity plugin API v6-v8; re-check names after SDK upgrades.
using System;
using TiltDrop.Core;
using UnityEngine;

namespace TiltDrop.Adapters
{
    public sealed class MaxAdsService : IAdsService
    {
#if UNITY_IOS
        const string RewardedId = "YOUR_IOS_REWARDED_AD_UNIT_ID";
        const string InterstitialId = "YOUR_IOS_INTERSTITIAL_AD_UNIT_ID";
#else
        const string RewardedId = "YOUR_ANDROID_REWARDED_AD_UNIT_ID";
        const string InterstitialId = "YOUR_ANDROID_INTERSTITIAL_AD_UNIT_ID";
#endif
        Action<bool> rewardedDone;
        bool rewardEarned;
        Action interstitialDone;
        int rewardedRetry, interRetry;
        MaxSdkBase.SdkConfiguration config;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => ServiceRegistry.Register<IAdsService>(() => new MaxAdsService());

        public void Initialize(PrivacyDecision d, Action<ConsentResult> done)
        {
            // The Terms & Privacy Policy flow (Google UMP consent form, then ATT on iOS) is enabled in
            // AppLovin > Integration Manager and runs automatically inside InitializeSdk().
            MaxSdkCallbacks.OnSdkInitializedEvent += cfg => MainThread.Post(() =>
            {
                config = cfg;
                Hook();
                MaxSdk.LoadRewardedAd(RewardedId);
                MaxSdk.LoadInterstitial(InterstitialId);
                done(ReadConsent(cfg));
            });
            MaxSdk.InitializeSdk();
        }

        static ConsentResult ReadConsent(MaxSdkBase.SdkConfiguration cfg)
        {
            var r = new ConsentResult { CanRequestAds = true };
            if (cfg.ConsentFlowUserGeography == MaxSdkBase.ConsentFlowUserGeography.Gdpr)
            {
                string p = Tcf.PurposeConsents();
                r.Personalized = Tcf.Has(p, 1) && Tcf.Has(p, 3) && Tcf.Has(p, 4);
                r.Analytics = Tcf.Has(p, 1) && Tcf.Has(p, 8);
                r.PrivacyOptionsRequired = true;
            }
            else
            {
                r.Personalized = true;
                r.Analytics = true;
            }
#if UNITY_IOS
            if (cfg.AppTrackingStatus != MaxSdkBase.AppTrackingStatus.Authorized) r.Personalized = false;
#endif
            return r;
        }

        void Hook()
        {
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (id, info) => rewardedRetry = 0;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (id, err) => MainThread.Post(() => Retry(ref rewardedRetry, () => MaxSdk.LoadRewardedAd(RewardedId)));
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (id, reward, info) => rewardEarned = true;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += (id, info) => MainThread.Post(() => FinishRewarded());
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += (id, err, info) => MainThread.Post(() => { rewardEarned = false; FinishRewarded(); });
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (id, info) => interRetry = 0;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (id, err) => MainThread.Post(() => Retry(ref interRetry, () => MaxSdk.LoadInterstitial(InterstitialId)));
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (id, info) => MainThread.Post(FinishInterstitial);
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += (id, err, info) => MainThread.Post(FinishInterstitial);
        }

        static void Retry(ref int attempt, Action load)
        {
            attempt = Math.Min(attempt + 1, 6);
            float delay = Mathf.Pow(2, attempt); // 2..64 s backoff
            var go = new GameObject("max-retry");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<DelayedCall>().Run(delay, load);
        }

        void FinishRewarded()
        {
            var cb = rewardedDone;
            rewardedDone = null;
            bool ok = rewardEarned;
            rewardEarned = false;
            MaxSdk.LoadRewardedAd(RewardedId);
            cb?.Invoke(ok);
        }

        void FinishInterstitial()
        {
            var cb = interstitialDone;
            interstitialDone = null;
            MaxSdk.LoadInterstitial(InterstitialId);
            cb?.Invoke();
        }

        public bool IsRewardedReady => MaxSdk.IsRewardedAdReady(RewardedId);
        public bool IsInterstitialReady => MaxSdk.IsInterstitialReady(InterstitialId);

        public void ShowRewarded(string placement, Action<bool> done)
        {
            if (!IsRewardedReady) { done(false); return; }
            rewardedDone = done;
            rewardEarned = false;
            MaxSdk.ShowRewardedAd(RewardedId, placement);
        }

        public void ShowInterstitial(string placement, Action done)
        {
            if (!IsInterstitialReady) { done(); return; }
            interstitialDone = done;
            MaxSdk.ShowInterstitial(InterstitialId, placement);
        }

        public bool PrivacyOptionsRequired => config != null && config.ConsentFlowUserGeography == MaxSdkBase.ConsentFlowUserGeography.Gdpr;

        public void ShowPrivacyOptions(Action done) => MaxSdk.CmpService.ShowCmpForExistingUser(error => MainThread.Post(() => done?.Invoke()));
    }

    /// <summary>Reads the IAB TCF v2.2 strings the CMP (Google UMP) stores on the device.</summary>
    static class Tcf
    {
        public static string PurposeConsents()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var pm = new AndroidJavaClass("android.preference.PreferenceManager"))
                using (var prefs = pm.CallStatic<AndroidJavaObject>("getDefaultSharedPreferences", activity))
                    return prefs.Call<string>("getString", "IABTCF_PurposeConsents", "");
            }
            catch (Exception) { return ""; }
#else
            return PlayerPrefs.GetString("IABTCF_PurposeConsents", ""); // iOS: NSUserDefaults
#endif
        }

        public static bool Has(string consents, int purpose) => consents != null && consents.Length >= purpose && consents[purpose - 1] == '1';
    }

    sealed class DelayedCall : MonoBehaviour
    {
        float at;
        Action action;
        public void Run(float delay, Action a) { at = Time.unscaledTime + delay; action = a; }
        void Update() { if (Time.unscaledTime >= at) { action?.Invoke(); Destroy(gameObject); } }
    }
}
