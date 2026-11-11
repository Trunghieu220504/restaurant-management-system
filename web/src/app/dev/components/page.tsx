import { notFound } from "next/navigation";
import { WeekOneDemo } from "@/features/operations/week-one-demo";
const demoStartedAt = new Date(Date.now() - 125000).toISOString();
export default function ComponentsDemoPage() {
    if (process.env.NODE_ENV !== "development") notFound();
    return <WeekOneDemo startedAt={demoStartedAt} />;
}
