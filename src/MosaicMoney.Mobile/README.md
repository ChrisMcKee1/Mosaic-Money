# Mosaic Money Mobile

This package contains the mobile app surface for Mosaic Money built with Expo SDK 55 and Expo Router.

Current MVP target is iPhone, but this is still mobile application development (not desktop app development).

## Implemented feature slice

- `MM-MOB-03`: NeedsReview queue screen with explicit pending status and pull-to-refresh behavior.
- `MM-MOB-04`: Transaction detail screen with read-only ledger truth fields and distinct `UserNote` / `AgentNote` lanes.
- `MM-MOB-05`: Human-in-the-loop approve/reject actions with explicit confirmation, backend `review-actions` routing, and sync-safe retry states.
- `MM-MOB-06.1`: Shared projection data hooks with runtime schema validation and resilient loading/error states.
- `MM-MOB-06.2`: Dashboard route scaffold for projection and balance summary presentation using read-only backend metadata.
- `MM-MOB-08`: Mobile Plaid Link onboarding flow using backend-issued `link_token` and backend `public_token` exchange.

## Plaid SDK runtime note

- The Plaid React Native SDK requires a native runtime (development build or release build).
- Expo Go does not include arbitrary native modules, so Plaid onboarding may not run there.
- Mobile still follows backend-first token exchange boundaries; no Plaid secret values are stored on device.

## Environment contract

Mobile API calls require a non-secret public endpoint:

- `EXPO_PUBLIC_API_BASE_URL`: Base URL for the Mosaic Money API.

Use `.env.example` as the contract template and keep real values in local-only env files.

For physical phone testing, set this to a host reachable by your phone:
- LAN mode example: `http://192.168.x.y:5001`
- Tunnel mode: use an internet-reachable API endpoint.

Do not place secrets in `EXPO_PUBLIC_*` values.

## Windows dev -> phone workflow

1. Start backend services (Aspire path).
2. Set `EXPO_PUBLIC_API_BASE_URL` to your reachable API URL.
3. Start the Expo dev server on Windows.
4. Open the app on your phone with Expo Go or a development build.

Recommended commands:

```bash
cd src/MosaicMoney.Mobile
npm install
npm run typecheck
npm run start:lan
```

If LAN networking is blocked or flaky, use tunnel mode:

```bash
npm run start:tunnel
```

## Local run

```bash
cd src/MosaicMoney.Mobile
npm install
npm run typecheck
npm run start
```

Notes:
- `npm run ios` requires a Mac-backed iOS simulator workflow.
- Windows-only teams can still complete day-to-day mobile development using physical phone testing and cloud/mobile build paths.

## Ship to phone workflow

Two common options:

1. Development testing on phone: Expo Go or development builds.
2. Installable artifact path: cloud iOS build/signing pipeline (for example EAS Build) and then install/test on provisioned phone devices.

Keep all sensitive operations and credentials in backend/AppHost secret paths. The mobile client should only receive public configuration and authenticated API surfaces.
