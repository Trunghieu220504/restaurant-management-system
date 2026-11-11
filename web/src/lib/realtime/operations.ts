export type ConnectionState = "offline" | "syncing" | "live";
export type OperationEvent<T> = {
    eventId: string;
    occurredAt: string;
    payload: T;
};
export interface OperationsTransport<T> {
    start(handlers: {
        event: (event: OperationEvent<T>) => void;
        reconnecting: () => void;
        reconnected: () => void;
        closed: () => void;
    }): Promise<void>;
    stop(): Promise<void>;
}
// Generation guards prevent stale snapshots from replacing a newer connection.
export class OperationsFeed<T> {
    private generation = 0;
    private seen = new Set<string>();
    private connectedAt = 0;
    private active = false;
    private ready = false;
    private buffered: OperationEvent<T>[] = [];
    constructor(
        private transport: OperationsTransport<T>,
        private snapshot: () => Promise<T[]>,
        private callbacks: {
            snapshot: (items: T[]) => void;
            event: (event: OperationEvent<T>, audible: boolean) => void;
            state: (state: ConnectionState) => void;
            error: (error: unknown) => void;
        },
        private capacity = 500,
        private now = () => Date.now(),
    ) {
        if (!Number.isInteger(capacity) || capacity < 1)
            throw new Error("Invalid capacity");
    }
    async start() {
        if (this.active) return;
        this.active = true;
        this.connectedAt = this.now();
        this.callbacks.state("syncing");
        try {
            await this.transport.start({
                event: (event) => this.receive(event),
                reconnecting: () => {
                    this.generation++;
                    this.ready = false;
                    this.buffered = [];
                    this.callbacks.state("offline");
                },
                reconnected: () => {
                    this.connectedAt = this.now();
                    void this.sync();
                },
                closed: () => {
                    this.generation++;
                    this.ready = false;
                    if (this.active) this.callbacks.state("offline");
                },
            });
            if (this.active) await this.sync();
        } catch (error) {
            if (this.active) {
                this.callbacks.state("offline");
                this.callbacks.error(error);
            }
        }
    }
    async stop() {
        this.active = false;
        this.ready = false;
        this.generation++;
        this.buffered = [];
        await this.transport.stop();
    }
    private async sync() {
        const generation = ++this.generation;
        this.ready = false;
        this.callbacks.state("syncing");
        try {
            const items = await this.snapshot();
            if (!this.active || generation !== this.generation) return;
            this.callbacks.snapshot(items);
            this.ready = true;
            this.callbacks.state("live");
            const pending = this.buffered;
            this.buffered = [];
            pending.forEach((event) => this.receive(event));
        } catch (error) {
            if (this.active && generation === this.generation) {
                this.callbacks.state("offline");
                this.callbacks.error(error);
            }
        }
    }
    private receive(event: OperationEvent<T>) {
        if (!this.active || !event.eventId) return;
        if (!this.ready) {
            if (this.buffered.length >= this.capacity) {
                this.buffered = [];
                void this.sync();
            }
            this.buffered.push(event);
            return;
        }
        if (this.seen.has(event.eventId)) return;
        this.seen.add(event.eventId);
        if (this.seen.size > this.capacity)
            this.seen.delete(this.seen.values().next().value!);
        const occurredAt = Date.parse(event.occurredAt);
        this.callbacks.event(
            event,
            Number.isFinite(occurredAt) && occurredAt > this.connectedAt,
        );
    }
}
