"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.OfflineCacheManager = exports.OfflineSyncManager = void 0;
class OfflineSyncManager {
    storage;
    queueKey = 'mosaic_money_sync_queue';
    isSyncing = false;
    constructor(storage) {
        this.storage = storage;
    }
    generateId() {
        return Date.now().toString(36) + Math.random().toString(36).substring(2);
    }
    async enqueue(type, payload) {
        const queue = await this.getQueue();
        const newOp = {
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
    async getQueue() {
        const data = await this.storage.getItem(this.queueKey);
        return data ? JSON.parse(data) : [];
    }
    async saveQueue(queue) {
        await this.storage.setItem(this.queueKey, JSON.stringify(queue));
    }
    async removeOperation(id) {
        const queue = await this.getQueue();
        const filtered = queue.filter(op => op.id !== id);
        await this.saveQueue(filtered);
    }
    async incrementRetry(id) {
        const queue = await this.getQueue();
        const op = queue.find(o => o.id === id);
        if (op) {
            op.retryCount += 1;
            await this.saveQueue(queue);
        }
    }
    async clearQueue() {
        await this.storage.removeItem(this.queueKey);
    }
    /**
     * Process the queue using the provided handler.
     * The handler should throw an error if the operation fails and should be retried.
     * If the handler returns successfully, the operation is removed from the queue.
     */
    async sync(handler) {
        if (this.isSyncing)
            return;
        this.isSyncing = true;
        try {
            const queue = await this.getQueue();
            for (const op of queue) {
                try {
                    await handler(op);
                    await this.removeOperation(op.id);
                }
                catch (error) {
                    await this.incrementRetry(op.id);
                    // Stop syncing on first failure to preserve order
                    break;
                }
            }
        }
        finally {
            this.isSyncing = false;
        }
    }
}
exports.OfflineSyncManager = OfflineSyncManager;
class OfflineCacheManager {
    storage;
    cachePrefix = 'mosaic_money_cache_';
    constructor(storage) {
        this.storage = storage;
    }
    getCacheKey(key) {
        return `${this.cachePrefix}${key}`;
    }
    async set(key, data) {
        await this.storage.setItem(this.getCacheKey(key), JSON.stringify(data));
    }
    async get(key) {
        const data = await this.storage.getItem(this.getCacheKey(key));
        return data ? JSON.parse(data) : null;
    }
    async remove(key) {
        await this.storage.removeItem(this.getCacheKey(key));
    }
}
exports.OfflineCacheManager = OfflineCacheManager;
