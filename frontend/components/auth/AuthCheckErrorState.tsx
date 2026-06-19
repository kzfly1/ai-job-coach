"use client";

import {Button} from "@/components/ui/button";

type AuthCheckErrorStateProps = {
    message: string;
};

export function AuthCheckErrorState({message}: AuthCheckErrorStateProps) {
    return (
        <main className="flex min-h-screen items-center justify-center bg-muted/30 px-4">
            <div className="max-w-md rounded-lg border bg-background p-6 text-center shadow-sm">
                <h1 className="text-lg font-semibold">
                    Unable to verify your session
                </h1>

                <p className="mt-2 text-sm text-muted-foreground">
                    {message}
                </p>

                <Button
                    className="mt-4"
                    onClick={() => window.location.reload()}
                >
                    Try again
                </Button>
            </div>
        </main>
    );
}