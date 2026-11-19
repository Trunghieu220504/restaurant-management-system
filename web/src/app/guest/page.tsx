import { redirect } from "next/navigation";
export default async function GuestPage({
    searchParams,
}: {
    searchParams: Promise<{ table?: string }>;
}) {
    const { table } = await searchParams;
    if (table) redirect("/guest/" + encodeURIComponent(table));
    return (
        <main className="p-5">
            <h1>Thực đơn tại bàn</h1>
            <p>Quét mã QR trên bàn để xem thực đơn.</p>
        </main>
    );
}
