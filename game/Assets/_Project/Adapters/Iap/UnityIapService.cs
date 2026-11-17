// Unity IAP adapter. Compiled only with TD_IAP. Written against the Unity IAP 4.x listener API
// (IDetailedStoreListener), which IAP 5 still ships as a compatibility layer; re-check after upgrades.
// Server-side receipt validation is recommended before scaling (docs/SETUP.md).
using System;
using System.Collections.Generic;
using TiltDrop.Core;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

namespace TiltDrop.Adapters
{
    public sealed class UnityIapService : IIapService, IDetailedStoreListener
    {
        IStoreController controller;
        IExtensionProvider extensions;
        Action<bool> initDone;
        Action<PurchaseOutcome> purchaseDone;

        public event Action<string> PurchaseGranted;
        public bool IsInitialized => controller != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => ServiceRegistry.Register<IIapService>(() => new UnityIapService());

        public async void Initialize(IList<ProductDef> products, Action<bool> done)
        {
            initDone = done;
            try { await UnityServices.InitializeAsync(); }
            catch (Exception e) { Debug.LogException(e); }
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            foreach (var p in products)
                builder.AddProduct(p.Id, p.Kind == ProductKind.NonConsumable ? ProductType.NonConsumable : ProductType.Consumable);
            UnityPurchasing.Initialize(this, builder);
        }

        public void OnInitialized(IStoreController c, IExtensionProvider e)
        {
            controller = c;
            extensions = e;
            initDone?.Invoke(true);
            initDone = null;
        }

        public void OnInitializeFailed(InitializationFailureReason error) => OnInitializeFailed(error, null);

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogWarning($"IAP init failed: {error} {message}");
            initDone?.Invoke(false);
            initDone = null;
        }

        public string LocalizedPrice(string id) => controller?.products.WithID(id)?.metadata.localizedPriceString;

        public bool IsOwned(string id)
        {
            var p = controller?.products.WithID(id);
            return p != null && p.hasReceipt;
        }

        public void Purchase(string id, Action<PurchaseOutcome> done)
        {
            if (controller == null) { done(PurchaseOutcome.Failed); return; }
            purchaseDone = done;
            controller.InitiatePurchase(id);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            // Also called at startup for purchases that completed while the app was closed.
            PurchaseGranted?.Invoke(args.purchasedProduct.definition.id);
            purchaseDone?.Invoke(PurchaseOutcome.Success);
            purchaseDone = null;
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription d) => Fail(d.reason);
        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason) => Fail(reason);

        void Fail(PurchaseFailureReason reason)
        {
            purchaseDone?.Invoke(reason == PurchaseFailureReason.UserCancelled ? PurchaseOutcome.Cancelled : PurchaseOutcome.Failed);
            purchaseDone = null;
        }

        public void Restore(Action<bool> done)
        {
            if (extensions == null) { done(false); return; }
#if UNITY_IOS
            extensions.GetExtension<IAppleExtensions>().RestoreTransactions((ok, msg) => done(ok));
#else
            extensions.GetExtension<IGooglePlayStoreExtensions>().RestoreTransactions((ok, msg) => done(ok));
#endif
        }
    }
}
