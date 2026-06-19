import Link from "next/link";

import { Button } from "@/components/ui/button";

export function ResumeListEmptyState() {
    return (
        <div className="rounded-lg border border-dashed bg-card p-10 text-center">
            <h2 className="text-base font-medium">No resumes yet</h2>
            <p className="mx-auto mt-1 max-w-sm text-sm text-muted-foreground">
                Upload your first resume to start building your AI job search profile.
            </p>

            <Button asChild className="mt-4">
                <Link href="/resumes/upload">Upload resume</Link>
            </Button>
        </div>
    );
}
