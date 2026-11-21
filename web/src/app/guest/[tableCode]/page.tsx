import { GuestMenu } from "@/features/guest/guest-menu";
export default async function GuestTable({
    params,
}: {
    params: Promise<{ tableCode: string }>;
}) {
    const { tableCode } = await params;
    return <GuestMenu key={tableCode} tableCode={tableCode} />;
}
