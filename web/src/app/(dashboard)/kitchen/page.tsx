import { KitchenDemo } from "@/features/kitchen/kitchen-demo";
import { KitchenIncidents } from "@/features/kitchen/kitchen-incidents";

export default function KitchenPage() {
    return (
        <section className="min-w-0 space-y-4">
            <h1 className="text-2xl font-semibold">Phiếu chế biến</h1>
            {process.env.NODE_ENV === "development" ? (
                <>
                    <p className="text-sm text-amber-900">
                        MÔ PHỎNG · Không có phiếu thật.
                    </p>
                    <KitchenDemo />
                </>
            ) : (
                <p
                    role="status"
                    className="border-l-4 border-amber-500 bg-amber-50 p-4 text-sm"
                >
                    Chưa kết nối được nguồn phiếu chế biến.
                </p>
            )}
            <KitchenIncidents />
        </section>
    );
}
