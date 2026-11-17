# Tilt & Drop — Compliance Design

> These are engineering design requirements, not legal advice. Before launch, have a Slovak/EU lawyer review the privacy policy, terms, and the consent and age flows. Re-check the store and ad-network policies at submission, because they change often.

Context: the developer (controller) is established in **Slovakia (EU)**. Supervisory authority: *Úrad na ochranu osobných údajov SR*. Relevant law: GDPR plus Slovak Act 18/2018 on personal data protection; ePrivacy as implemented in Slovak Act 452/2021 on electronic communications (§109, storage/access on devices); consumer law (Consumer Rights Directive, UCPD, Omnibus; Slovak Act 108/2024 on consumer protection); DSA (trader information on app stores).

## 1. Data inventory

| Data | Purpose | Legal basis | Who | 16+ & consent | 16+ no consent | Under 16 |
|---|---|---|---|---|---|---|
| Ad ID (GAID/IDFA), IP, device info | personalized ads, frequency capping, attribution | consent (TCF purposes 1,3,4,7…) / ATT on iOS | AppLovin MAX + networks, AdMob | yes | non-personalized only (TCF, limited ads) | **never** (§3) |
| App-instance ID, gameplay events | analytics, balancing | consent (via Google Consent Mode, mapped from UMP) | Firebase Analytics | yes | collection off | off |
| Crash traces, device model, OS | stability | legitimate interest, with opt-out in Settings (*confirm with counsel; fallback: tie to consent*) | Firebase Crashlytics | yes | yes (opt-out) | off |
| Purchase receipts | deliver purchases, restore | contract | Apple/Google, Unity IAP | — | — | no purchases |
| Local save (progress, coins, settings, age band, consent state) | the game | contract / strictly necessary | device only | yes | yes | yes |

No accounts, no names, no emails, no precise location, no contacts, no chat. Settings has **Delete my data**: it wipes the local save, resets the Firebase installation ID, and calls the analytics deletion API where available. Settings also has a **Privacy settings** entry that re-opens the consent form (required by UMP when `privacyOptionsRequirementStatus == REQUIRED`).

## 2. Consent (GDPR / TCF v2.2) and ATT

- Google requires a **Google-certified CMP integrated with IAB TCF v2.2** to serve ads in the EEA, UK, and Switzerland. We use **Google UMP** (a certified CMP), triggered through **AppLovin MAX's Terms & Privacy Policy Flow**. MAX runs UMP and then ATT in the right order and passes the TC string to the mediated networks.
- First-launch order: **Splash → neutral age screen → (16+) UMP consent → (iOS, 16+) ATT pre-prompt explainer → system ATT prompt → game.**
- No ad request goes out before `consentInfoUpdate` completes and `canRequestAds == true`. MAX and Firebase consent mode are both set from the UMP result before the first ad or analytics event.
- Firebase Consent Mode v2: `ad_storage`, `ad_user_data`, `ad_personalization`, and `analytics_storage` are derived from TCF purposes 1/3/4/7/8/9.
- Outside the EEA, UMP also shows the **US state regulations** message (CPRA etc., "Do not sell or share") where applicable.
- **ATT:** asked only for users 16+, after UMP, never incentivized and never forced. The pre-prompt is neutral (no fake "Allow" button styling) and the game works fully if the user declines. `NSUserTrackingUsageDescription` example: *"Your data will be used to show you more relevant ads."* SKAdNetwork IDs come from the MAX integration manager. A **privacy manifest** (`PrivacyInfo.xcprivacy`) is included for the app, and the SDKs ship their own.
- Consent records: the TC string is stored by UMP and our save keeps `consentState + timestamp`. Re-consent happens when UMP requires it (vendor list changes, 13 months).

## 3. Neutral age screen and the under-16 path

- **Screen:** "When were you born?" with a year picker (plus month when the year is the threshold year). Nothing is pre-selected, there's no hint about the threshold or what each answer leads to, and no "I am 16+" button. The answer is stored and not re-asked. Changing it later only allows a *younger* band (to prevent gaming the gate). It is re-asked after a reinstall.
- **Threshold 16** everywhere. That's Slovakia's GDPR Art. 8 age and the strictest in the EU, and one global rule keeps it simple. Under 13 gets the same treatment, plus `tagForChildDirectedTreatment`.
- **Under-16 behavior:**
  - **Ads: non-personalized and contextual only.** Under AppLovin's current policy (MAX SDK 13+ removed the age-restricted-user API; AppLovin asks publishers not to initialize its SDK for known children; *verify at implementation*), **AppLovin MAX is not initialized**. Instead the Google Mobile Ads SDK (already in the build as the MAX AdMob adapter dependency) is used directly with `tagForUnderAgeOfConsent = true`, `maxAdContentRating = G`, and non-personalized requests. **Fallback** if that proves impractical: no ad SDK at all for under-16, with rewarded actions replaced by a small free daily allowance.
  - **Paid purchases off:** store, piggy bank, starter pack, and Remove Ads are hidden. No purchase prompts anywhere. Coins come from gameplay only.
  - Firebase Analytics collection disabled, Crashlytics disabled, no ATT prompt, no IDFA/GAID access.
  - Everything else (all levels, daily puzzle, calendar, album) is fully available. **Purchases are never needed to progress for anyone** (every level is solver-verified to be solvable within its move limit without boosters).
- **Store positioning:** Google Play target audience 13+ (not "Designed for Families"). Apple age rating from the questionnaire (expected 4+/9+), not in the Kids category. If we later target under-13s, the Families policy and a Families-self-certified ad SDK list apply. That's a separate project.

## 4. Virtual currency (CPC Network key principles on in-game virtual currencies, 2024)

| Principle | Implementation |
|---|---|
| Don't obscure the real-money cost | **One** soft currency, "coins". Every store product shows its **price in €** (from the store's localized price). Coin-priced items also show "≈ €x.xx" (at the Coins S rate) |
| No unnecessary intermediate currencies or unclear conversions | No second, premium currency, no bundles in odd units, fixed and visible conversion |
| Avoid forced leftover balances | Pack sizes are aligned with sink prices (100/150/200 multiples). No "you need 20 more coins" upsell traps |
| Clear pre-contractual information | Every pack lists exact contents before the purchase sheet. The rules for piggy bank contents are shown |
| Right of withdrawal | Store purchase flows obtain consent for immediate supply. Our terms explain it. Refunds go through Apple/Google |
| No aggressive or exploitative practices | No timers on offers, no confirmshaming, equally prominent "No thanks", no purchase pop-ups after a fail. The continue offer may show coins/ad only, never a paid pack directly |
| Protect minors | Under-16: no purchases at all. No direct exhortations to children ("Buy now!") anywhere |
| Purchased coins don't expire | Coins never expire and survive updates. Non-consumables can be restored |

## 5. EU Digital Fairness Act proposal (Nov 2026): design commitments

Built in now, regardless of the final text:

1. **No paid random rewards:** no loot boxes, gacha, mystery chests for coins or money, or random sticker packs. Every coin or € purchase has fully known contents.
2. **No sold spins or wheels.** The daily reward is a **fixed login calendar** (no wheel, no ad-boosted multiplier, no paid re-spin).
3. **No pay-to-progress for minors:** under-16 has no purchases, and for everyone, levels are solvable without boosters within the move limit.
4. **No pressure timers:** no countdown on offers, continue prompts, or events (events show an end *date*), no energy/lives, no "only X left", no fake scarcity.
5. **No dark patterns:** symmetric choices, one-tap close, no pre-ticked options, no nagging. At most 1 store suggestion per session, never after a fail.
6. **Streaks without coercion:** a free weekly daily-streak freeze, no paid streak saves, gentle wording when a streak ends.
7. **Notifications (post-launch):** opt-in, at most 1 per day, no guilt-trip copy, quiet hours.
8. **No real-world prize contests or sweepstakes**, ever. Leaderboards (post-launch) are for bragging rights only.
9. **Transparency:** an in-game "How the game earns money" page (ads + optional purchases, what's personalized).

## 6. Store-specific checklist (before submission)

- **Google Play:** Data safety form (matches §1), ads declaration "contains ads", target audience 13+, IARC rating, privacy policy URL, DSA trader verification, closed test (12 testers × 14 days) for new personal accounts, current target API level, the declaration about the AD_ID permission.
- **Apple:** App Privacy "nutrition labels" (tracking: yes for 16+ consenting users), privacy manifest, ATT string, age-rating questionnaire, Paid Apps agreement + tax/banking, DSA trader status (address/phone/email shown on the EU storefront), IAP products reviewed with the first build, "Restore Purchases" button.
- **EAA (European Accessibility Act):** microenterprises are exempt for services, but we still provide color symbols, scalable text, reduced motion, and haptics/sound toggles.
- **Privacy policy and terms** hosted on our own domain, linked from the store listings and in-game Settings. They name the controller and contact details, every SDK/vendor, legal bases, retention, rights (access, erasure, objection, complaint to ÚOOÚ SR), and international transfers (the EU–US Data Privacy Framework / standard contractual clauses (SCCs) via Google and AppLovin).
