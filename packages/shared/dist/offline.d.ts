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
export declare class OfflineSyncManager {
    private readonly storage;
    private readonly queueKey;
    private isSyncing;
    constructor(storage: StorageProvider);
    private generateId;
    enqueue<T>(type: string, payload: T): Promise<SyncOperation<T>>;
    getQueue(): Promise<SyncOperation[]>;
    private saveQueue;
    removeOperation(id: string): Promise<void>;
    incrementRetry(id: string): Promise<void>;
    clearQueue(): Promise<void>;
    /**
     * Process the queue using the provided handler.
     * The handler should throw an error if the operation fails and should be retried.
     * If the handler returns successfully, the operation is removed from the queue.
     */
    sync(handler: (op: SyncOperation) => Promise<void>): Promise<void>;
}
export declare class OfflineCacheManager {
    private readonly storage;
    private readonly cachePrefix;
    constructor(storage: StorageProvider);
    private getCacheKey;
    set<T>(key: string, data: T): Promise<void>;
    get<T>(key: string): Promise<T | null>;
    remove(key: string): Promise<void>;
}
