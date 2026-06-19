"use client";

import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";

import {isUnauthorizedError} from "@/lib/api-error";
import {getMe, logout as logoutRequest} from "@/lib/auth-api";
import type {UserProfileDto} from "@/types/api";

export const authQueryKeys = {
    me: ["auth", "me"] as const,
};

// retry: 401 -> no retry; network/500 -> retry twice
export function useAuth() {
    const queryClient = useQueryClient();

    const meQuery = useQuery<UserProfileDto, unknown>({
        queryKey: authQueryKeys.me,
        queryFn: getMe,
        retry: (failureCount, error) => {
            if (isUnauthorizedError(error)) {
                return false;
            }

            return failureCount < 2;
        },
    });

    const isUnauthorized = isUnauthorizedError(meQuery.error);
    const hasAuthCheckFailure = meQuery.isError && !isUnauthorized;

    // Important:
    // Do not trust stale cached user data after /api/auth/me returns 401.
    const user = isUnauthorized ? null : (meQuery.data ?? null);

    const logoutMutation = useMutation({
        mutationFn: logoutRequest,
        onSuccess: () => {
            queryClient.removeQueries({queryKey: ["auth"]});
        },
    });

    return {
        user,
        isLoading: meQuery.isLoading,
        isError: meQuery.isError,
        error: meQuery.error,
        isUnauthorized,
        hasAuthCheckFailure,
        isAuthenticated: Boolean(user),
        logout: logoutMutation.mutateAsync,
        isLoggingOut: logoutMutation.isPending,
    };
}