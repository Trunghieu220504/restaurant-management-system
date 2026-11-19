import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { SeatingChange, SeatingGateway } from "./seating";
const boardKey = ["seating", "board"] as const;
export function useSeatingBoard(gateway: SeatingGateway) {
    return useQuery({
        queryKey: boardKey,
        queryFn: ({ signal }) => gateway.load(signal),
        retry: false,
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
