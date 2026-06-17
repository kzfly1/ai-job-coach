type ResumeUploadProgressProps = {
    progress: number;
};

export function ResumeUploadProgress({ progress }: ResumeUploadProgressProps) {
    return (
        <div className="space-y-2">
            <div className="h-2 overflow-hidden rounded-full bg-muted">
                <div
                    className="h-full bg-foreground transition-all"
                    style={{ width: `${progress}%` }}
                />
            </div>

            <p className="text-sm text-muted-foreground">
                Upload progress: {progress}%
            </p>
        </div>
    );
}
