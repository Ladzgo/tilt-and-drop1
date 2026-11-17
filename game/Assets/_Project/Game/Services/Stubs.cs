using System;
using System.Collections.Generic;
using TiltDrop.Core;
using UnityEngine;

namespace TiltDrop.Game
{
    /// <summary>Editor/offline stand-ins. Real adapters (Adapters/*) replace these when their SDK + define are present.</summary>
    public sealed class StubAds : IAdsService
    {
        public void Initialize(PrivacyDecision d, Action<ConsentResult> done)
        {
            Debug.Log($"[ads-stub] init max={d.InitMax} admobDirect={d.InitAdMobDirect} personalized={d.PersonalizedAds}");
            done(new ConsentResult { CanRequestAds = true, Personalized = d.PersonalizedAds, Analytics = true });
        }

        public bool IsRewardedReady => true;
        public bool IsInterstitialReady => true;

        public void ShowRewarded(string placement, Action<bool> done)
        {
            Debug.Log($"[ads-stub] rewarded '{placement}' -> granted");
            Tweener.Delay(0.4f, () => done(true));
        }

        public void ShowInterstitial(string placement, Action done)
        {
            Debug.Log($"[ads-stub] interstitial '{placement}'");
            Tweener.Delay(0.3f, done);
        }

        public bool PrivacyOptionsRequired => false;
        public void ShowPrivacyOptions(Action done) => done?.Invoke();
    }

    public sealed class StubIap : IIapService
    {
        readonly HashSet<string> owned = new HashSet<string>();
        IList<ProductDef> products = new List<ProductDef>();
        public event Action<string> PurchaseGranted;
        public bool IsInitialized { get; private set; }

        public void Initialize(IList<ProductDef> p, Action<bool> done) { products = p; IsInitialized = true; done(true); }

        public string LocalizedPrice(string id)
        {
            foreach (var p in products) if (p.Id == id) return p.FallbackPriceEur;
            return null;
        }

        public void Purchase(string id, Action<PurchaseOutcome> done)
        {
            Debug.Log($"[iap-stub] purchase {id}");
            var def = StoreCatalog.Find(id);
            if (def != null && def.Kind == ProductKind.NonConsumable) owned.Add(id);
            Tweener.Delay(0.3f, () => { PurchaseGranted?.Invoke(id); done(PurchaseOutcome.Success); });
        }

        public void Restore(Action<bool> done) => done(true);
        public bool IsOwned(string id) => owned.Contains(id);
    }

    public sealed class StubAnalytics : IAnalytics
    {
        bool enabled;
        public void SetEnabled(bool e) => enabled = e;

        public void Log(string name, IDictionary<string, object> p = null)
        {
            if (!enabled || !Debug.isDebugBuild) return;
            Debug.Log($"[analytics-stub] {name} {(p == null ? "" : MiniJson.Serialize(p))}");
        }
    }

    public sealed class StubRemoteConfig : IRemoteConfig
    {
        public void Fetch(IDictionary<string, string> defaults, Action<IDictionary<string, string>> done) => done(defaults);
    }

    public sealed class StubCrash : ICrashReporter
    {
        public void SetEnabled(bool e) { }
        public void Log(string m) { }
        public void Record(Exception e) => Debug.LogException(e);
    }
}
