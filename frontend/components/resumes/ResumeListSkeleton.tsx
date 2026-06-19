export function ResumeListSkeleton() {
    return (
        <div className="space-y-2" aria-busy="true" aria-live="polite">
            {[0, 1, 2].map((row) => (
                <div
                    key={row}
                    className="flex items-center justify-between rounded-lg border bg-card p-4"
                >
                    <div className="space-y-2">
                        <div className="h-4 w-48 animate-pulse rounded bg-muted" />
                        <div className="h-3 w-32 animate-pulse rounded bg-muted" />
                    </div>

                    <div className="h-8 w-20 animate-pulse rounded bg-muted" />
                </div>
            ))}
        </div>
    );
}
