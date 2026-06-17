import {ResumeUploadForm} from "@/components/resumes/ResumeUploadForm";

export default function ResumeUploadPage() {
    return (
        <div className="mx-auto max-w-2xl space-y-6">
            <div>
                <h1 className="text-2xl font-semibold tracking-tight">
                    Upload resume
                </h1>
                <p className="mt-1 text-sm text-muted-foreground">
                    Upload a PDF or DOCX resume to start building your AI job search
                    profile.
                </p>
            </div>

            <ResumeUploadForm/>
        </div>
    );
}