import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { SeatingChange, SeatingGateway } from "./seating";
import { liveInterval } from "@/lib/realtime/live-query";
const boardKey = ["seating", "board"] as const;
export function useSeatingBoard(gateway: SeatingGateway, live: boolean) {
    return useQuery({
        queryKey: boardKey,
        queryFn: ({ signal }) => gateway.load(signal),
        retry: false,
        refetchInterval: live ? liveInterval : false,
    });
}
export function useSeatingChange(gateway: SeatingGateway) {
    const client = useQueryClient();
    return useMutation({
        mutationFn: ({ change, key }: { change: SeatingChange; key: string }) =>
            gateway.apply(change, key),
        retry: false,
        onSuccess: () => client.invalidateQueries({ queryKey: boardKey }),
    });
}
