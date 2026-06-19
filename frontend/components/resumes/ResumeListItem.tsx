import { Button } from "@/components/ui/button";
import { formatDate } from "@/lib/formatters/date";
import type { ResumeDto } from "@/types/api";

type ResumeListItemProps = {
    resume: ResumeDto;
    onRequestDelete: (resume: ResumeDto) => void;
};

export function ResumeListItem({ resume, onRequestDelete }: ResumeListItemProps) {
    return (
        <div className="flex items-center justify-between gap-4 rounded-lg border bg-card p-4">
            <div className="min-w-0">
                <p className="truncate text-sm font-medium">{resume.fileName}</p>
                <p className="mt-1 text-xs text-muted-foreground">
                    Uploaded {formatDate(resume.uploadedAt)}
                </p>
            </div>

            <Button
                type="button"
                variant="destructive"
                size="sm"
                onClick={() => onRequestDelete(resume)}
            >
                Delete
            </Button>
        </div>
    );
}
