import { notFound } from "next/navigation";
import { SeatingPreview } from "@/features/seating/seating-preview";

export default function SeatingPreviewPage() {
    if (process.env.NODE_ENV !== "development") notFound();
    return <SeatingPreview />;
}
