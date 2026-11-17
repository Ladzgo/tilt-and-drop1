using System;
using System.Collections.Generic;

namespace TiltDrop.Core
{
    public struct ConsentResult
    {
        public bool CanRequestAds;
        public bool Personalized;   // TCF purposes 1, 3, 4 (or outside GDPR scope)
        public bool Analytics;      // TCF purposes 1, 8 (or outside GDPR scope)
        public bool PrivacyOptionsRequired;
    }

    /// <summary>Ads + the consent flow that gates them (AppLovin MAX runs UMP, then ATT).</summary>
    public interface IAdsService
    {
        /// <summary>Initializes the right SDK for the decision (MAX for 16+, AdMob-direct for under 16)
        /// and gathers consent where required. Callback on the main thread.</summary>
        void Initialize(PrivacyDecision decision, Action<ConsentResult> done);
        bool IsRewardedReady { get; }
        bool IsInterstitialReady { get; }
        /// <summary>done(true) only when the reward was earned.</summary>
        void ShowRewarded(string placement, Action<bool> done);
        void ShowInterstitial(string placement, Action done);
        bool PrivacyOptionsRequired { get; }
        void ShowPrivacyOptions(Action done);
    }

    public enum PurchaseOutcome { Success, Cancelled, Failed, Pending }

    public interface IIapService
    {
        bool IsInitialized { get; }
        void Initialize(IList<ProductDef> products, Action<bool> done);
        /// <summary>Localized price string from the store, or null if unknown.</summary>
        string LocalizedPrice(string productId);
        void Purchase(string productId, Action<PurchaseOutcome> done);
        void Restore(Action<bool> done);
        bool IsOwned(string productId);
    }

    public interface IAnalytics
    {
        void SetEnabled(bool enabled);
        void Log(string name, IDictionary<string, object> parameters = null);
    }

    public interface IRemoteConfig
    {
        void Fetch(IDictionary<string, string> defaults, Action<IDictionary<string, string>> done);
    }

    public interface ICrashReporter
    {
        void SetEnabled(bool enabled);
        void Log(string message);
        void Record(Exception e);
    }

    /// <summary>Real SDK adapters register factories here (from their own assemblies, before the
    /// first scene loads); anything not registered falls back to the stubs.</summary>
    public static class ServiceRegistry
    {
        static readonly Dictionary<Type, Func<object>> Factories = new Dictionary<Type, Func<object>>();

        public static void Register<T>(Func<T> factory) where T : class => Factories[typeof(T)] = () => factory();

        public static T Resolve<T>(Func<T> fallback) where T : class =>
            Factories.TryGetValue(typeof(T), out var f) ? (T)f() : fallback();

        public static bool IsRegistered<T>() => Factories.ContainsKey(typeof(T));
        public static void Clear() => Factories.Clear();
    }
}
