import { HubConnectionBuilder } from "@microsoft/signalr";
import type { OperationEvent, OperationsTransport } from "./operations";
export function createSignalRTransport<T>(
    url: string,
    eventName: string,
    accessTokenFactory: () => string | Promise<string>,
): OperationsTransport<T> {
    const hub = new HubConnectionBuilder()
        .withUrl(url, { accessTokenFactory })
        .withAutomaticReconnect()
        .build();
    return {
        async start(handlers) {
            hub.on(eventName, (event: OperationEvent<T>) =>
                handlers.event(event),
            );
            hub.onreconnecting(handlers.reconnecting);
            hub.onreconnected(handlers.reconnected);
            hub.onclose(handlers.closed);
            await hub.start();
        },
        async stop() {
            hub.off(eventName);
            await hub.stop();
        },
    };
}
