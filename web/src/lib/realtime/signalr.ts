import { HubConnectionBuilder } from "@microsoft/signalr";
import type { OperationEvent, OperationsTransport } from "./operations";
export function createSignalRTransport<T>(
    url: string,
    eventName: string,
    accessTokenFactory: () => string | Promise<string>,
    subscriptions: readonly { method: string; args?: unknown[] }[] = [],
): OperationsTransport<T> {
    const hub = new HubConnectionBuilder()
        .withUrl(url, { accessTokenFactory })
        .withAutomaticReconnect()
        .build();
    async function subscribe() {
        for (const item of subscriptions)
            await hub.invoke(item.method, ...(item.args ?? []));
    }
    return {
        async start(handlers) {
            hub.on(eventName, (event: OperationEvent<T>) =>
                handlers.event(event),
            );
            hub.onreconnecting(handlers.reconnecting);
            hub.onreconnected(async () => {
                try {
                    await subscribe();
                    handlers.reconnected();
                } catch {
                    handlers.closed();
                }
            });
            hub.onclose(handlers.closed);
            await hub.start();
            await subscribe();
        },
        async stop() {
            hub.off(eventName);
            await hub.stop();
        },
    };
}
