import { useEffect, useState } from "react";
import Image from "next/image";
import { Button } from "@/components/ui/button";
import { authFetch } from "@/lib/auth/auth-client";

export function GuestInviteButton({
    sessionId,
    name,
}: {
    sessionId: string;
    name: string;
}) {
    const [invite, setInvite] = useState<{
        image: string;
        expires: number;
    } | null>(null);
    const [busy, setBusy] = useState(false),
        [error, fail] = useState("");
    useEffect(() => {
        if (!invite) return;
        const t = setTimeout(
            () => setInvite(null),
            Math.max(0, invite.expires - Date.now()),
        );
        return () => clearTimeout(t);
    }, [invite]);
    async function issue() {
        setBusy(true);
        fail("");
        try {
            const res = await authFetch(
                `/api/seating/sessions/${sessionId}/guest-access`,
                { method: "POST", cache: "no-store" },
            );
            if (!res.ok) throw Error();
            const data = await res.json();
            const url = new URL(
                "/guest/" + encodeURIComponent(data.tableCode),
                location.origin,
            );
            url.hash = new URLSearchParams({ j: data.joinCode }).toString();
            const qr = await import("qrcode");
            setInvite({
                image: await qr.toDataURL(url.href, { width: 256, margin: 4 }),
                expires: Date.parse(data.expiresAt),
            });
        } catch {
            fail("Không thể cấp mã. Kiểm tra lại phiên bàn.");
        } finally {
            setBusy(false);
        }
    }
    return (
        <div>
            <Button
                size="sm"
                variant="outline"
                disabled={busy}
                onClick={() => (invite ? setInvite(null) : void issue())}
            >
                {invite ? "Ẩn mã QR" : "Mời khách qua QR"}
            </Button>
            {invite && (
                <Image
                    unoptimized
                    src={invite.image}
                    alt={"Mã QR tham gia " + name}
                    width={256}
                    height={256}
                />
            )}
            {error && <p role="alert">{error}</p>}
        </div>
    );
}
