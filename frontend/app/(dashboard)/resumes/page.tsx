import Link from "next/link";

import { ResumeList } from "@/components/resumes/ResumeList";
import { Button } from "@/components/ui/button";

export default function ResumesPage() {
    return (
        <div className="space-y-6">
            <div className="flex items-start justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-semibold tracking-tight">Resumes</h1>
                    <p className="mt-1 text-sm text-muted-foreground">
                        Manage your uploaded resumes.
                    </p>
                </div>

                <Button asChild>
                    <Link href="/resumes/upload">Upload resume</Link>
                </Button>
            </div>

            <ResumeList />
        </div>
    );
}
