"use client";

import {useAuth} from "@/lib/hooks/use-auth";
import {useRouter} from "next/navigation";
import type {ReactNode} from "react";
import {useEffect} from "react";
import Link from "next/link";
import {Button} from "@/components/ui/button";
import {AuthCheckErrorState} from "@/components/auth/AuthCheckErrorState";
import {getApiErrorMessage} from "@/lib/api-error";

export default function DashboardLayout({children,}: {
    children: ReactNode;
}) {
    const router = useRouter();

    const {user, isLoading, hasAuthCheckFailure, error, logout, isLoggingOut} = useAuth();

    const authCheckErrorMessage = getApiErrorMessage(
        error,
        "The authentication service is currently unavailable. Please try again."
    );

    useEffect(() => {
        if (isLoading || user || hasAuthCheckFailure) {
            return;
        }
        router.replace("/login");
    }, [hasAuthCheckFailure, isLoading, user, router]);

    async function handleLogout() {
        await logout();
        router.replace("/login");
    }

    if (isLoading) {
        return (
            <main className="flex min-h-screen items-center justify-center">
                <p className="text-sm text-muted-foreground">Loading...</p>
            </main>
        )
    }

    if (hasAuthCheckFailure) {
        return <AuthCheckErrorState message={authCheckErrorMessage}/>;
    }

    if (!user) {
        return null;
    }

    return (
        <div className="min-h-screen bg-muted/30">
            <header className="border-b bg-background">
                <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-6">
                    <div>
                        <p className="text-sm font-semibold">AI Job Coach</p>
                        <p className="text-xs text-muted-foreground">
                            {user.fullName || user.email}
                        </p>
                    </div>

                    <nav className="flex items-center gap-4">
                        <Link href="/dashboard" className="text-sm font-medium text-foreground hover:underline">
                            Dashboard
                        </Link>

                        <Link href="/resumes" className="text-sm font-medium text-foreground hover:underline">
                            Resumes
                        </Link>

                        <Button variant="outline" size="sm" onClick={handleLogout} disabled={isLoggingOut}>
                            {isLoggingOut ? "Logging out..." : "Logout"}
                        </Button>
                    </nav>
                </div>
            </header>

            <main className="mx-auto max-w-6xl px-6 py-8">{children}</main>
        </div>
    );
}
