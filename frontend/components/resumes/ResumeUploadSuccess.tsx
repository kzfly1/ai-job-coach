type ResumeUploadSuccessProps = {
    fileName: string;
};

export function ResumeUploadSuccess({ fileName }: ResumeUploadSuccessProps) {
    return (
        <div className="rounded-md border border-green-200 bg-green-50 p-4 text-sm text-green-900">
            <p className="font-medium">Resume uploaded successfully.</p>
            <p className="mt-1">Uploaded file: {fileName}</p>
        </div>
    );
}
