export class RequestAttempts {
    private values = new Map<string, { fingerprint: string; key: string }>();
    begin(operation: string, payload: unknown): string {
        const fingerprint = JSON.stringify(payload);
        let attempt = this.values.get(operation);
        if (attempt?.fingerprint !== fingerprint) {
            attempt = { fingerprint, key: crypto.randomUUID() };
            this.values.set(operation, attempt);
        }
        return attempt!.key;
    }
    complete(operation: string, key: string) {
        if (this.values.get(operation)?.key === key)
            this.values.delete(operation);
    }
}
