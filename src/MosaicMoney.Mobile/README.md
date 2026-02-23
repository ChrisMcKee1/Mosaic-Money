# Mosaic Money Mobile (Scaffold)

This folder contains the initial Expo TypeScript scaffold for the Mosaic Money mobile app.

## Current scope

- Establishes an executable mobile runtime surface at `src/MosaicMoney.Mobile`.
- Unblocks mobile feature tasks that were previously blocked by missing project scaffold.
- Uses Expo tooling generated via `create-expo-app` and aligned to Expo SDK 55 preview dependencies.

## Next implementation tasks

- Build NeedsReview queue screen (`MM-MOB-03`).
- Build transaction detail with dual notes (`MM-MOB-04`).
- Add shared contract integration from `packages/shared` where applicable.

## Local run

```bash
cd src/MosaicMoney.Mobile
npm install
npm run start
```
