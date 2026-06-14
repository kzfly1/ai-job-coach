"use client";

import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";

import {getMe, logout as logoutRequest} from "@/lib/auth-api";

export const authQueryKeys = {
    me: ["auth", "me"] as const,
};

export function useAuth() {
    const queryClient = useQueryClient();

    const meQuery = useQuery({
        queryKey: authQueryKeys.me,
        queryFn: getMe,
        retry: false,
    });

    const logoutMutation = useMutation({
        mutationFn: logoutRequest,
        onSuccess: () => {
            queryClient.removeQueries({queryKey: ["auth"]});
        }
    });

    return {
        user: meQuery.data ?? null,
        isLoading: meQuery.isLoading,
        isError: meQuery.isError,
        isAuthenticated: Boolean(meQuery.data),
        logout: logoutMutation.mutateAsync,
        isLoggingOut: logoutMutation.isPending,
    }
}