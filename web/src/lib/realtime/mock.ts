import type { OperationsTransport, OperationEvent } from "./operations";
export class MockOperationsTransport<T> implements OperationsTransport<T> {
    private handlers?: Parameters<OperationsTransport<T>["start"]>[0];
    async start(handlers: Parameters<OperationsTransport<T>["start"]>[0]) {
        this.handlers = handlers;
    }
    async stop() {
        this.handlers = undefined;
    }
    emit(event: OperationEvent<T>) {
        this.handlers?.event(event);
    }
    disconnect() {
        this.handlers?.reconnecting();
    }
    reconnect() {
        this.handlers?.reconnected();
    }
}
