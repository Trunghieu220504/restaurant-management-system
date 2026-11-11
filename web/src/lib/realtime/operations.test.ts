import { describe, expect, it, vi } from "vitest";
import { OperationsFeed } from "./operations";
import { MockOperationsTransport } from "./mock";
function setup(snapshot = vi.fn(async () => ["snapshot"])) {
    const transport = new MockOperationsTransport<string>();
    const callbacks = {
        snapshot: vi.fn(),
        event: vi.fn(),
        state: vi.fn(),
        error: vi.fn(),
    };
    const feed = new OperationsFeed(transport, snapshot, callbacks, 2, () =>
        Date.now(),
    );
    return { transport, callbacks, feed, snapshot };
}
describe("operations feed", () => {
    it("deduplicates event IDs and does not emit snapshot sounds", async () => {
        vi.useFakeTimers();
        vi.setSystemTime(new Date("2026-11-10T00:00:00Z"));
        const { transport, callbacks, feed } = setup();
        await feed.start();
        expect(callbacks.event).not.toHaveBeenCalled();
        const event = {
            eventId: "1",
            occurredAt: "2026-11-10T00:00:01Z",
            payload: "new",
        };
        transport.emit(event);
        transport.emit(event);
        expect(callbacks.event).toHaveBeenCalledExactlyOnceWith(event, true);
        await feed.stop();
        vi.useRealTimers();
    });
    it("reloads after reconnect and suppresses historical events", async () => {
        vi.useFakeTimers();
        vi.setSystemTime(new Date("2026-11-10T00:00:00Z"));
        const { transport, callbacks, feed, snapshot } = setup();
        await feed.start();
        transport.disconnect();
        vi.advanceTimersByTime(10000);
        transport.reconnect();
        await Promise.resolve();
        const event = {
            eventId: "old",
            occurredAt: "2026-11-10T00:00:05Z",
            payload: "old",
        };
        transport.emit(event);
        expect(snapshot).toHaveBeenCalledTimes(2);
        expect(callbacks.event).toHaveBeenCalledWith(event, false);
        await feed.stop();
        vi.useRealTimers();
    });
    it("buffers live events while snapshot loads", async () => {
        let resolve!: (items: string[]) => void;
        const { transport, callbacks, feed } = setup(
            vi.fn(
                () =>
                    new Promise<string[]>((r) => {
                        resolve = r;
                    }),
            ),
        );
        const start = feed.start();
        await Promise.resolve();
        transport.emit({
            eventId: "1",
            occurredAt: new Date(Date.now() + 1000).toISOString(),
            payload: "queued",
        });
        expect(callbacks.event).not.toHaveBeenCalled();
        resolve(["snapshot"]);
        await start;
        expect(callbacks.event).toHaveBeenCalledTimes(1);
        await feed.stop();
    });
    it("ignores snapshots completing after stop", async () => {
        let resolve!: (items: string[]) => void;
        const { callbacks, feed } = setup(
            vi.fn(
                () =>
                    new Promise<string[]>((r) => {
                        resolve = r;
                    }),
            ),
        );
        const start = feed.start();
        await Promise.resolve();
        await feed.stop();
        resolve(["late"]);
        await start;
        expect(callbacks.snapshot).not.toHaveBeenCalled();
    });
    it("surfaces snapshot failure without announcing live", async () => {
        const { callbacks, feed } = setup(
            vi.fn(async () => {
                throw new Error("offline");
            }),
        );
        await feed.start();
        expect(callbacks.error).toHaveBeenCalledTimes(1);
        expect(callbacks.state).toHaveBeenLastCalledWith("offline");
        await feed.stop();
    });
});
