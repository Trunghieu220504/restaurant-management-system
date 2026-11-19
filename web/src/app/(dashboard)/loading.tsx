import { Skeleton } from "@/components/ui/skeleton";

export default function DashboardLoading() {
    return (
        <div className="space-y-8" aria-label="Đang tải nội dung">
            <div className="space-y-2">
                <Skeleton className="h-4 w-36" />
                <Skeleton className="h-8 w-48" />
                <Skeleton className="h-4 w-72" />
            </div>
            <div className="grid gap-4 md:grid-cols-3">
                {Array.from({ length: 3 }, (_, index) => (
                    <Skeleton key={index} className="h-32 rounded-lg" />
                ))}
            </div>
            <Skeleton className="h-72 rounded-lg" />
        </div>
    );
}
