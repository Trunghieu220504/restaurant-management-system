export interface AudioDriver {
    unlock(): Promise<void>;
    play(): Promise<void>;
    close(): Promise<void>;
}
export type SoundState = {
    muted: boolean;
    blocked: boolean;
    unlocked: boolean;
};
export class NotificationSound {
    private state: SoundState = {
        muted: false,
        blocked: false,
        unlocked: false,
    };
    constructor(
        private audio: AudioDriver,
        private storage?: Pick<Storage, "getItem" | "setItem">,
        private changed: (state: SoundState) => void = () => {},
    ) {
        try {
            this.state.muted =
                storage?.getItem("restaurant.ops.muted") === "true";
        } catch {
            /* Storage can be disabled. */
        }
    }
    get current() {
        return { ...this.state };
    }
    private publish() {
        this.changed(this.current);
    }
    async unlock() {
        try {
            await this.audio.unlock();
            this.state.unlocked = true;
            this.state.blocked = false;
        } catch {
            this.state.blocked = true;
        }
        this.publish();
    }
    toggleMute() {
        this.state.muted = !this.state.muted;
        try {
            this.storage?.setItem(
                "restaurant.ops.muted",
                String(this.state.muted),
            );
        } catch {
            /* Retain in memory. */
        }
        this.publish();
    }
    async notify() {
        if (this.state.muted) return;
        if (!this.state.unlocked) {
            this.state.blocked = true;
            this.publish();
            return;
        }
        try {
            await this.audio.play();
            this.state.blocked = false;
        } catch {
            this.state.blocked = true;
        }
        this.publish();
    }
    dispose() {
        return this.audio.close();
    }
}
export function createBrowserAudio(): AudioDriver {
    let context: AudioContext | undefined;
    return {
        async unlock() {
            context ??= new AudioContext();
            await context.resume();
            if (context.state !== "running") throw new Error("Audio blocked");
        },
        async play() {
            if (!context || context.state !== "running")
                throw new Error("Audio blocked");
            const oscillator = context.createOscillator();
            const gain = context.createGain();
            oscillator.frequency.value = 880;
            gain.gain.setValueAtTime(0.12, context.currentTime);
            gain.gain.exponentialRampToValueAtTime(
                0.001,
                context.currentTime + 0.2,
            );
            oscillator.connect(gain);
            gain.connect(context.destination);
            oscillator.start();
            oscillator.stop(context.currentTime + 0.2);
            oscillator.onended = () => {
                oscillator.disconnect();
                gain.disconnect();
            };
        },
        async close() {
            if (context && context.state !== "closed") await context.close();
        },
    };
}
