"use client";

import {useRouter} from "next/navigation";
import {useEffect} from "react";

import {useAuth} from "@/lib/hooks/use-auth";

export function useRedirectAuthenticatedUser(redirectTo = "/dashboard") {
    const router = useRouter();
    const {user, isLoading} = useAuth();

    useEffect(() => {
        if (!isLoading && user) {
            router.replace(redirectTo);
        }
    }, [isLoading, user, redirectTo, router]);

    return {
        user,
        isLoading,
        shouldRender: !isLoading && !user,
    }
}