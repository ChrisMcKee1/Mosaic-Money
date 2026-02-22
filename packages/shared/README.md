# @mosaic-money/shared

Shared domain contracts and API client for Mosaic Money frontend applications (Web and Mobile).

## Integration Path

### For Next.js Web App
1. Add the dependency to `src/MosaicMoney.Web/package.json`:
   ```json
   "dependencies": {
     "@mosaic-money/shared": "file:../../packages/shared"
   }
   ```
2. Import and use the client:
   ```typescript
   import { MosaicMoneyApiClient } from '@mosaic-money/shared';

   const client = new MosaicMoneyApiClient({
     baseUrl: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000',
   });

   const transactions = await client.getTransactions();
   ```

### For React Native (Expo) Mobile App
1. Once the mobile app is created (e.g., in `src/MosaicMoney.Mobile`), add the dependency:
   ```json
   "dependencies": {
     "@mosaic-money/shared": "file:../../packages/shared"
   }
   ```
2. Import and use the client:
   ```typescript
   import { MosaicMoneyApiClient } from '@mosaic-money/shared';

   const client = new MosaicMoneyApiClient({
     baseUrl: process.env.EXPO_PUBLIC_API_URL || 'http://localhost:5000',
     getToken: async () => {
       // Retrieve token from secure storage
       return 'your-auth-token';
     }
   });

   const transactions = await client.getTransactions();
   ```

## Offline Foundation

The `@mosaic-money/shared` package provides a queue-friendly offline cache and sync baseline for mobile workflows.

### Integration

1. Implement the `StorageProvider` interface using your platform's storage mechanism (e.g., `AsyncStorage` for React Native, `localStorage` for Web).

```typescript
import { StorageProvider } from '@mosaic-money/shared';
import AsyncStorage from '@react-native-async-storage/async-storage';

export const asyncStorageProvider: StorageProvider = {
  getItem: async (key: string) => AsyncStorage.getItem(key),
  setItem: async (key: string, value: string) => AsyncStorage.setItem(key, value),
  removeItem: async (key: string) => AsyncStorage.removeItem(key),
};
```

2. Use `OfflineCacheManager` to cache read queries:

```typescript
import { OfflineCacheManager } from '@mosaic-money/shared';

const cache = new OfflineCacheManager(asyncStorageProvider);

// Save data to cache
await cache.set('transactions', transactionsData);

// Retrieve data from cache
const cachedTransactions = await cache.get('transactions');
```

3. Use `OfflineSyncManager` to queue mutations when offline:

```typescript
import { OfflineSyncManager, SyncOperation } from '@mosaic-money/shared';

const syncManager = new OfflineSyncManager(asyncStorageProvider);

// Enqueue a mutation
await syncManager.enqueue('CREATE_TRANSACTION', { amount: 100, description: 'Groceries' });

// Process the queue when online
await syncManager.sync(async (op: SyncOperation) => {
  switch (op.type) {
    case 'CREATE_TRANSACTION':
      await apiClient.createTransaction(op.payload);
      break;
    // Handle other operation types
    default:
      throw new Error(`Unknown operation type: ${op.type}`);
  }
});
```

## Development
To build the package:
```bash
npm install
npm run build
```

To run type checking:
```bash
npm run typecheck
```
