import { notFound } from "next/navigation";
import { WeekThreeDemo } from "@/features/operations/week-three-demo";
export default function WeekThreePage() {
    if (process.env.NODE_ENV !== "development") notFound();
    return <WeekThreeDemo />;
}
