export interface StorageProvider {
  getItem(key: string): Promise<string | null>;
  setItem(key: string, value: string): Promise<void>;
  removeItem(key: string): Promise<void>;
}

export interface SyncOperation<T = any> {
  id: string;
  type: string;
  payload: T;
  timestamp: string;
  retryCount: number;
}

export class OfflineSyncManager {
  private readonly storage: StorageProvider;
  private readonly queueKey = 'mosaic_money_sync_queue';
  private isSyncing = false;

  constructor(storage: StorageProvider) {
    this.storage = storage;
  }

  private generateId(): string {
    return Date.now().toString(36) + Math.random().toString(36).substring(2);
  }

  async enqueue<T>(type: string, payload: T): Promise<SyncOperation<T>> {
    const queue = await this.getQueue();
    const newOp: SyncOperation<T> = {
      id: this.generateId(),
      type,
      payload,
      timestamp: new Date().toISOString(),
      retryCount: 0,
    };
    queue.push(newOp);
    await this.saveQueue(queue);
    return newOp;
  }

  async getQueue(): Promise<SyncOperation[]> {
    const data = await this.storage.getItem(this.queueKey);
    return data ? JSON.parse(data) : [];
  }

  private async saveQueue(queue: SyncOperation[]): Promise<void> {
    await this.storage.setItem(this.queueKey, JSON.stringify(queue));
  }

  async removeOperation(id: string): Promise<void> {
    const queue = await this.getQueue();
    const filtered = queue.filter(op => op.id !== id);
    await this.saveQueue(filtered);
  }

  async incrementRetry(id: string): Promise<void> {
    const queue = await this.getQueue();
    const op = queue.find(o => o.id === id);
    if (op) {
      op.retryCount += 1;
      await this.saveQueue(queue);
    }
  }

  async clearQueue(): Promise<void> {
    await this.storage.removeItem(this.queueKey);
  }

  /**
   * Process the queue using the provided handler.
   * The handler should throw an error if the operation fails and should be retried.
   * If the handler returns successfully, the operation is removed from the queue.
   */
  async sync(handler: (op: SyncOperation) => Promise<void>): Promise<void> {
    if (this.isSyncing) return;
    this.isSyncing = true;

    try {
      const queue = await this.getQueue();
      for (const op of queue) {
        try {
          await handler(op);
          await this.removeOperation(op.id);
        } catch (error) {
          await this.incrementRetry(op.id);
          // Stop syncing on first failure to preserve order
          break;
        }
      }
    } finally {
      this.isSyncing = false;
    }
  }
}

export class OfflineCacheManager {
  private readonly storage: StorageProvider;
  private readonly cachePrefix = 'mosaic_money_cache_';

  constructor(storage: StorageProvider) {
    this.storage = storage;
  }

  private getCacheKey(key: string): string {
    return `${this.cachePrefix}${key}`;
  }

  async set<T>(key: string, data: T): Promise<void> {
    await this.storage.setItem(this.getCacheKey(key), JSON.stringify(data));
  }

  async get<T>(key: string): Promise<T | null> {
    const data = await this.storage.getItem(this.getCacheKey(key));
    return data ? JSON.parse(data) : null;
  }

  async remove(key: string): Promise<void> {
    await this.storage.removeItem(this.getCacheKey(key));
  }
}
