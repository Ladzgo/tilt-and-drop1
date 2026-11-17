# Tilt & Drop — Setup, Build & Release Guide

This is every manual step, in order. Steps marked ⏳ have lead times, so start them early (see ROADMAP.md, "Start now").

Contents
1. [Install Unity](#1-install-unity)
2. [Open and play in the Editor](#2-open-and-play-in-the-editor)
3. [Build and run on an Android phone (Windows/Linux/Mac PC)](#3-android-build-on-your-pc)
4. [Build and run on iPhone (Mac or cloud)](#4-ios-build-mac-or-cloud)
5. [Integrate the SDKs (MAX, AdMob, Firebase, Unity IAP)](#5-sdk-integration)
6. [CI: GitHub Actions secrets](#6-ci-github-actions-secrets)
7. [Store accounts and submission](#7-store-accounts)
8. [Release checklist](#8-release-checklist)
9. [Level pipeline commands](#9-level-pipeline)

---

## 1. Install Unity

1. Install **Unity Hub**: https://unity.com/download
2. Sign in with a Unity ID. Activate a **Unity Personal** license (Hub → Preferences → Licenses → Add). Personal is free while revenue is under the current threshold.
3. Hub → Installs → Install Editor → choose the **latest Unity 6 LTS** (6000.3.x at the time of writing). Add these modules:
   - **Android Build Support**, including **OpenJDK** and **Android SDK & NDK Tools** (tick both sub-items)
   - **iOS Build Support** (exporting the Xcode project works on Windows/Linux too; building the app needs a Mac)
4. Optional: **Visual Studio Code** + the C# Dev Kit, or JetBrains Rider.

> `game/ProjectSettings/ProjectVersion.txt` pins `6000.3.0f1` as a placeholder. If Hub says the version isn't installed, open the project with your installed 6000.x LTS editor and confirm the upgrade. Unity rewrites the file; **commit it**, because CI uses it to pick the matching editor image.

## 2. Open and play in the Editor

1. Hub → Projects → **Add → Add project from disk** → choose the `game/` folder of this repository (not the repo root).
2. The first import takes a few minutes. On first load the project:
   - copies `/shared/levels` into `Assets/_Project/Resources/Levels` (menu: **Tilt & Drop → Sync Level Packs**),
   - creates/repairs the empty scene `Assets/_Project/Scenes/Main.unity` and registers it in the build settings,
   - applies all player settings (menu: **Tilt & Drop → Apply Project Settings**): bundle id, portrait, IL2CPP, ARM64, AAB, min SDK 24, iOS 15, iPhone-only.

   If Unity shows *"Active Input Handling changed, restart?"*, click **Yes**.
3. Set the Game view to a phone resolution (e.g. *1080x2400 Portrait*).
4. Press **Play**. A fresh profile starts with the age screen, then level 1. Controls in the Editor:
   - swipe with the mouse (drag left/right) or press **← / →** (also **A / D**),
   - click the arrow buttons at the bottom.
5. Run the tests: **Window → General → Test Runner → EditMode → Run All**. These are the same tests CI runs with `dotnet test`, including the Python↔C# golden vectors.
6. To reset your local profile: in-game **Settings → Delete my data**, or delete `save.json` in `Application.persistentDataPath`.

Everything (board, UI, sounds, sprites) is generated from code. There are no prefabs to wire up. SDK calls use stubs until you do step 5: rewarded ads are granted instantly and purchases "succeed" without charging.

## 3. Android build on your PC

1. On the phone: Settings → About → tap **Build number** 7× → Developer options → enable **USB debugging**. Connect by USB and accept the RSA prompt.
2. Unity: **File → Build Profiles → Android → Switch Platform** (the first switch takes a while).
3. **Create an upload keystore** (once, and **back it up**: losing it means losing the ability to update on some setups):
   Player Settings → Android → Publishing Settings → **Keystore Manager → Create New → Anywhere**. Save it **outside the repo** (e.g. `~/keys/tiltdrop-upload.keystore`) with an alias like `upload`. Record the passwords in a password manager.
4. For a quick test, uncheck *Build App Bundle* (APK) temporarily, or leave AAB for Play uploads. Then **Build And Run** with the phone selected under *Run Device*.
5. Store builds: keep **Build App Bundle (Google Play)** on (ProjectSetup sets it). Upload the `.aab` to Play Console (§7). Enable **Play App Signing**: Google holds the app signing key and your keystore is the *upload* key.
6. Version: bump `Player Settings → Version` (e.g. 0.1.1) and **Bundle Version Code** (+1) for every upload. CI does this automatically.

Troubleshooting:
- "SDK/NDK not found" → re-run Hub's module installer with the Android sub-modules ticked.
- Device not listed → `adb devices` (in the Unity-bundled SDK `platform-tools`) must show it as `device`.

## 4. iOS build (Mac or cloud)

**On a Mac (Xcode, latest version required by App Store Connect):**
1. Unity: **File → Build Profiles → iOS → Switch Platform → Build** → choose an output folder (e.g. `build/iOS`).
2. Open `Unity-iPhone.xcodeproj` in Xcode → target **Unity-iPhone** → *Signing & Capabilities* → tick **Automatically manage signing**, select your **Team**.
3. Connect an iPhone (enable Developer Mode on the phone: Settings → Privacy & Security → Developer Mode) → press **Run**.
4. TestFlight: Product → **Archive** → Distribute App → App Store Connect → Upload. Then in App Store Connect → TestFlight, add internal testers.

**No Mac?** Use the `build iOS` GitHub Actions workflow. Job 1 exports the Xcode project on Linux. Job 2 runs on a GitHub macOS runner and uses fastlane to sign and upload to TestFlight (secrets in §6, one-time *match* setup below). Alternatives: Unity Build Automation or Codemagic.

**One-time fastlane match setup** (needs any Mac once, or a cloud Mac session):
```bash
brew install fastlane
```
```bash
cd game && fastlane match init
```
```bash
cd game && fastlane match appstore --app_identifier com.laszlo.tiltdrop
```
`match init` asks for a **private** git repo URL for certificates; use that as `MATCH_GIT_URL`. `match appstore` creates the distribution certificate and profile, encrypted with the passphrase you choose (`MATCH_PASSWORD`).

## 5. SDK integration

The project compiles and runs with **no SDKs**. Each real adapter lives in `Assets/_Project/Adapters/<Name>/`, compiles only when its scripting define is set, and registers itself automatically. Add defines in **Player Settings → Other Settings → Scripting Define Symbols** for **both Android and iOS**.

| SDK | Define | Adapter |
|---|---|---|
| AppLovin MAX (16+ users; runs Google UMP consent + ATT) | `TD_MAX` | `Adapters/Max/MaxAdsService.cs` |
| Google Mobile Ads plugin (under-16, non-personalized) | `TD_ADMOB` | `Adapters/AdMob/AdMobChildSafeAds.cs` |
| Firebase Analytics + Remote Config + Crashlytics | `TD_FIREBASE` | `Adapters/Firebase/FirebaseServices.cs` |
| Unity IAP | `TD_IAP` | `Adapters/Iap/UnityIapService.cs` |

> The adapters are written against the documented APIs (MAX Unity plugin 6–8, GMA Unity 9, Firebase Unity 11/12, Unity IAP 4.x listener API) but could not be compiled here without the SDKs. If a newer SDK renamed something, the compiler points at the exact line. They're short files, so fix them in place.

### 5.1 AppLovin MAX + AdMob mediation (`TD_MAX`)
1. Create an AppLovin account → MAX → add the apps (Android `com.laszlo.tiltdrop`, iOS same bundle id).
2. Download the **AppLovin MAX Unity Plugin** `.unitypackage` from the dashboard → Assets → Import Package.
3. **AppLovin → Integration Manager**:
   - paste the **SDK key**,
   - install mediated networks: **Google bidding and Google AdMob** (required), plus optionally Unity Ads, Meta Audience Network, Mintegral, ironSource, …,
   - enter the **AdMob App IDs** (Android and iOS) from your AdMob account,
   - enable **Terms and Privacy Policy Flow** → set your *Privacy Policy URL* (and Terms URL), and enable the **Google UMP** CMP option. MAX then shows the UMP consent form (TCF v2.2) and afterwards the iOS ATT prompt, in that order. Set the ATT *user tracking usage description*.
4. In **AdMob → Privacy & messaging**: create and **publish** the **European regulations (GDPR)** message, the **IDFA explainer** (iOS), and the **US states** message. UMP shows whatever is published there.
5. MAX dashboard → create ad units: **Rewarded** and **Interstitial**, for Android and iOS → paste the IDs at the top of `MaxAdsService.cs`.
6. Host **app-ads.txt** on your developer website domain (the URL in your store listings) with the lines from AppLovin and AdMob.
7. Add the define `TD_MAX`. Run **Assets → External Dependency Manager → Android Resolver → Force Resolve** (EDM4U is bundled with the plugin).

### 5.2 Google Mobile Ads plugin for under-16 users (`TD_ADMOB`)
Per COMPLIANCE.md §3, MAX is **not initialized** for under-16 users. They get Google ads with under-age tags (non-personalized, G-rated). If you skip this SDK, under-16 users simply see **no ads**, which is the compliant fallback.
1. Import the **Google Mobile Ads Unity plugin** release (github.com/googleads/googleads-mobile-unity). Use a version whose native SDK matches the AdMob adapter MAX installed; after importing, *Force Resolve* and check there's only one `play-services-ads` / `Google-Mobile-Ads-SDK` version.
2. Assets → Google Mobile Ads → Settings: enter the same AdMob App IDs.
3. AdMob → create separate **Rewarded** and **Interstitial** units (Android/iOS) → paste them into `AdMobChildSafeAds.cs`.
4. Add the define `TD_ADMOB`.

### 5.3 Firebase (`TD_FIREBASE`)
1. console.firebase.google.com → new project (Analytics on, choose an EU data location).
2. Add an Android app (`com.laszlo.tiltdrop`) and an iOS app (same bundle id). Download `google-services.json` and `GoogleService-Info.plist` into `game/Assets/`. Keep them out of public repos (add them to `.gitignore`); CI injects them from secrets.
3. Download the **Firebase Unity SDK** → import `FirebaseAnalytics`, `FirebaseRemoteConfig`, `FirebaseCrashlytics` (same version) → *Force Resolve*.
4. **Collection off until consent**:
   - iOS: handled automatically by `Editor/IosPostProcess.cs` when `TD_FIREBASE` is set.
   - Android: create `Assets/Plugins/Android/FirebaseConsent.androidlib/AndroidManifest.xml` containing:
     ```xml
     <manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.laszlo.tiltdrop.consent">
       <application>
         <meta-data android:name="firebase_analytics_collection_enabled" android:value="false" />
         <meta-data android:name="firebase_crashlytics_collection_enabled" android:value="false" />
         <meta-data android:name="google_analytics_default_allow_ad_personalization_signals" android:value="false" />
       </application>
     </manifest>
     ```
     and next to it a `project.properties` file containing `android.library=true`.
   The game enables collection at runtime only when the age and consent decision allows it.
5. **Remote Config**: create the parameters from TECH.md §7.2 (defaults ship in code, so only add what you want to tune). Difficulty can be adjusted per level with `difficulty_overrides` = `{"57": 9}` (level → move limit).
6. Add the define `TD_FIREBASE`.

### 5.4 Unity IAP (`TD_IAP`)
1. **Window → Package Manager → Unity Registry → In-App Purchasing → Install**.
2. **Edit → Project Settings → Services**: link the project to a Unity Cloud project (needed for Unity Gaming Services init).
3. Create the products **with exactly these IDs** (see `Core/Meta/Store.cs`):

   | Product ID | Type (Play / Apple) | Price |
   |---|---|---|
   | `com.laszlo.tiltdrop.removeads` | Managed non-consumable / Non-Consumable | €4.99 |
   | `com.laszlo.tiltdrop.starter` | Consumable / Consumable | €1.99 |
   | `com.laszlo.tiltdrop.noadsbundle` | Non-consumable / Non-Consumable | €7.99 |
   | `com.laszlo.tiltdrop.coins1` … `coins5` | Consumable | €1.99 / 4.99 / 9.99 / 19.99 / 49.99 |
   | `com.laszlo.tiltdrop.piggy` | Consumable | €3.99 |

   - Google Play Console → *Monetize → Products → In-app products* (needs a merchant account and an uploaded AAB that includes the billing library, which Unity IAP adds).
   - App Store Connect → your app → *In-App Purchases* (needs the Paid Apps agreement). Set the **Euro (Slovakia) price** and let Apple auto-equalize other storefronts.
4. Recommended before scaling: **receipt validation** (IAP → Receipt Validation Obfuscator, Google Play public key), or server-side validation.
5. Add the define `TD_IAP`. Test with Play **license testers** and Apple **Sandbox** accounts.

## 6. CI: GitHub Actions secrets

Repository → Settings → Secrets and variables → Actions → *New repository secret*.

| Secret | How to get it |
|---|---|
| `UNITY_LICENSE` | Activate Unity Personal in Hub on your PC, then copy the **contents** of the license file: Windows `C:\ProgramData\Unity\Unity_lic.ulf`, macOS `/Library/Application Support/Unity/Unity_lic.ulf`, Linux `~/.local/share/unity3d/Unity/Unity_lic.ulf` (see game.ci → Activation for the current procedure) |
| `UNITY_EMAIL`, `UNITY_PASSWORD` | Your Unity ID login |
| `ANDROID_KEYSTORE_BASE64` | `base64 -w0 tiltdrop-upload.keystore` (macOS: `base64 -i file`) |
| `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS` | From step 3.3 |
| `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` (optional) | Play Console → Setup → API access → link a Google Cloud project → create a service account → JSON key; in Play Console *Users & permissions*, invite the service account email with release rights for the app |
| `GOOGLE_SERVICES_JSON_BASE64`, `GOOGLE_SERVICE_INFO_PLIST_BASE64` (optional) | base64 of the two Firebase files |
| `APPLE_TEAM_ID` | developer.apple.com → Membership |
| `ASC_KEY_ID`, `ASC_ISSUER_ID`, `ASC_KEY_P8_BASE64` | App Store Connect → Users and Access → Integrations → App Store Connect API → generate a key (App Manager role); base64 the downloaded `.p8` |
| `MATCH_GIT_URL`, `MATCH_PASSWORD`, `MATCH_GIT_BASIC_AUTHORIZATION` | From the match setup (§4); the last one is `base64("github-user:personal-access-token")` with read access to the certificates repo |

Workflows:
- `levelgen (python)` and `core (C#, no Unity)` run on every push and need no secrets.
- `unity edit-mode tests`, `build android (AAB)`, `build iOS` are **manual** (Actions tab → Run workflow) or run on tags `v*`. Android: tick *upload_internal* to push a draft to the Play internal track. iOS: tick *testflight* to sign and upload.

## 7. Store accounts

### 7.1 Google Play ⏳
1. https://play.google.com/console → pay **$25** once. Choose **Organization** (needs a D-U-N-S number) or **Personal**.
   - **Personal accounts created after 13 Nov 2023** must run a **closed test with at least 12 opted-in testers for 14 consecutive days** before they can apply for production access. Start it as soon as a stable AAB exists (ROADMAP week 5). Organization accounts are exempt.
2. Identity and address verification. EU **DSA trader declaration**: you're a trader (you monetize), so your address, phone and email are shown publicly.
3. Create the app → complete **App content**: privacy policy URL, **Ads: yes**, **Target audience: 13–15, 16–17, 18+** (not under 13, not "Designed for Families"), **Data safety** (match COMPLIANCE.md §1), IARC content rating questionnaire, news app no, government no, financial features no.
4. Testing: Internal testing (instant) → Closed testing (testers via email list or Google Group) → after 14 days apply for **Production**.
5. Store listing: icon 512×512, feature graphic 1024×500, at least 2 phone screenshots, short/full description (EN, plus SK/CZ/DE later).
6. Payments profile + merchant account (for IAP), EU VAT handled by Google.

### 7.2 Apple App Store ⏳
1. https://developer.apple.com/programs → **€99/year**. An organization needs a legal entity + **D-U-N-S** (free lookup via Apple; takes days).
2. App Store Connect → **Agreements, Tax, and Banking**: accept **Paid Apps**, add your bank account and tax forms (W-8BEN for individuals or W-8BEN-E for companies, claiming the SK–US treaty rate).
3. Certificates, Identifiers & Profiles → **Identifiers** → App ID `com.laszlo.tiltdrop` (capability: In-App Purchase).
4. App Store Connect → **My Apps → +** → new iOS app with that bundle id.
5. **App Privacy** questionnaire (tracking: yes, only for consenting 16+ users; data types per COMPLIANCE.md §1), **age rating** questionnaire, category Games → Puzzle, **DSA trader status** for the EU storefront, export compliance (no non-exempt encryption; the plist key is set automatically).
6. Screenshots: 6.9" iPhone set (the project is iPhone-only, so no iPad set is needed). Add a 15–30 s app preview made from cascade footage.
7. Submit the IAPs together with the first build. Add a **Restore Purchases** button (already in Settings).

## 8. Release checklist

- [ ] Replace the placeholder bundle id `com.laszlo.tiltdrop` (ProjectSetup.cs, Store.cs product ids, workflows, Fastfile) if you choose another
- [ ] `GameRoot.PrivacyPolicyUrl` → your hosted privacy policy (also in the MAX Integration Manager and the store listings)
- [ ] Real ad unit IDs (MAX, AdMob) and SDK keys; app-ads.txt live
- [ ] Defines `TD_MAX;TD_ADMOB;TD_FIREBASE;TD_IAP` set for Android **and** iOS
- [ ] App icon (Player Settings → Icon) and adaptive icon; product name
- [ ] Remote Config published with the defaults; `ad_inter_*` verified
- [ ] UMP messages published (GDPR, IDFA explainer, US states); tested with a VPN/EEA test device (AdMob test devices)
- [ ] Sandbox / license-tester purchases for every SKU, plus restore
- [ ] Data safety (Play) and App Privacy (Apple) match COMPLIANCE.md
- [ ] Version name/code bumped; tag `v0.x.y` → CI builds
- [ ] Legal review of privacy policy, terms and consent flow (Slovak/EU lawyer)

## 9. Level pipeline

From `tools/levelgen` (Python ≥ 3.10, no dependencies; `pip install pytest pytest-cov` for tests). Details are in docs/LEVELS.md.

```bash
python -m pytest --cov
```
```bash
python -m tiltdrop levels --first 1 --last 300 --k 12
```
```bash
python -m tiltdrop levels --first 301 --last 20000 --k-pool 2 --final-bot-runs 100
```
```bash
python -m tiltdrop daily --count 400
```
```bash
python -m tiltdrop pack --curated 300 --daily-epoch 2027-01-01
```
```bash
python -m tiltdrop validate --sample 25
```
```bash
python -m tiltdrop golden --check
```
After `pack`, run **Tilt & Drop → Sync Level Packs** in Unity (it also runs automatically on editor start and in CI builds).
