import { expect, it, vi } from "vitest";
import { NotificationSound } from "./notification-sound";
function driver() {
    return {
        unlock: vi.fn(async () => {}),
        play: vi.fn(async () => {}),
        close: vi.fn(async () => {}),
    };
}
it("reports blocked audio before user activation and never replays it on unlock", async () => {
    const audio = driver();
    const sound = new NotificationSound(audio);
    await sound.notify();
    expect(sound.current.blocked).toBe(true);
    expect(audio.play).not.toHaveBeenCalled();
    await sound.unlock();
    expect(audio.play).not.toHaveBeenCalled();
    await sound.notify();
    expect(audio.play).toHaveBeenCalledTimes(1);
});
it("persists mute and respects it", async () => {
    const audio = driver();
    const storage = { getItem: vi.fn(() => "true"), setItem: vi.fn() };
    const sound = new NotificationSound(audio, storage);
    await sound.unlock();
    await sound.notify();
    expect(audio.play).not.toHaveBeenCalled();
    sound.toggleMute();
    expect(storage.setItem).toHaveBeenCalledWith(
        "restaurant.ops.muted",
        "false",
    );
    await sound.notify();
    expect(audio.play).toHaveBeenCalledTimes(1);
});
it("reports failed playback and tolerates blocked storage", async () => {
    const audio = driver();
    audio.play.mockRejectedValue(new Error("blocked"));
    const sound = new NotificationSound(audio, {
        getItem() {
            throw new Error();
        },
        setItem() {
            throw new Error();
        },
    });
    await sound.unlock();
    await sound.notify();
    expect(sound.current.blocked).toBe(true);
    expect(() => sound.toggleMute()).not.toThrow();
    await sound.dispose();
    expect(audio.close).toHaveBeenCalled();
});
