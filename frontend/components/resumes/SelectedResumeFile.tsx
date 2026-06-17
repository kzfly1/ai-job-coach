import { formatResumeFileSize } from "@/lib/validations/resume-file";

type SelectedResumeFileProps = {
    file: File;
};

export function SelectedResumeFile({ file }: SelectedResumeFileProps) {
    return (
        <div className="rounded-md border p-4 text-sm">
            <p className="font-medium">{file.name}</p>
            <p className="text-muted-foreground">
                {formatResumeFileSize(file.size)}
            </p>
        </div>
    );
}
