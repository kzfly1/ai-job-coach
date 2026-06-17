"use client";

import {type ComponentProps, useState} from "react";

import {ResumeDropzone} from "@/components/resumes/ResumeDropzone";
import {ResumeUploadProgress} from "@/components/resumes/ResumeUploadProgress";
import {ResumeUploadSuccess} from "@/components/resumes/ResumeUploadSuccess";
import {SelectedResumeFile} from "@/components/resumes/SelectedResumeFile";
import {FormErrorMessage} from "@/components/shared/FormErrorMessage";
import {Button} from "@/components/ui/button";
import {Card, CardContent, CardHeader, CardTitle} from "@/components/ui/card";
import {useUploadResume} from "@/lib/hooks/use-resumes";
import {validateResumeFile} from "@/lib/validations/resume-file";
import type {ApiClientError, ResumeDto} from "@/types/api";

type FormSubmitHandler = NonNullable<ComponentProps<"form">>["onSubmit"];

function getApiErrorMessage(error: unknown): string {
    const apiError = error as Partial<ApiClientError>;

    if (typeof apiError.message === "string" && apiError.message.length > 0) {
        return apiError.message;
    }

    return "Resume upload failed. Please try again.";
}

export function ResumeUploadForm() {
    const [selectedFile, setSelectedFile] = useState<File | null>(null);
    const [validationError, setValidationError] = useState<string | null>(null);
    const [progress, setProgress] = useState(0);
    const [uploadedResume, setUploadedResume] = useState<ResumeDto | null>(null);

    const uploadMutation = useUploadResume();

    function resetUploadState() {
        setProgress(0);
        setUploadedResume(null);
        uploadMutation.reset();
    }

    function handleFileSelected(file: File) {
        resetUploadState();

        const error = validateResumeFile(file);

        if (error) {
            setSelectedFile(null);
            setValidationError(error);
            return;
        }

        setValidationError(null);
        setSelectedFile(file);
    }

    const handleSubmit: FormSubmitHandler = (event) => {
        event.preventDefault();

        if (!selectedFile) {
            setValidationError("Please choose a resume file first.");
            return;
        }

        setValidationError(null);
        setProgress(0);
        setUploadedResume(null);

        uploadMutation.mutate(
            {
                file: selectedFile,
                onProgress: setProgress,
            },
            {
                onSuccess: (resume) => {
                    setProgress(100);
                    setUploadedResume(resume);
                },
            },
        );
    };

    const uploadError = uploadMutation.isError
        ? getApiErrorMessage(uploadMutation.error)
        : null;

    return (
        <Card>
            <CardHeader>
                <CardTitle>Upload resume</CardTitle>
            </CardHeader>

            <CardContent>
                <form onSubmit={handleSubmit} className="space-y-6">
                    <ResumeDropzone onFileSelected={handleFileSelected}/>

                    {selectedFile ? (
                        <SelectedResumeFile file={selectedFile}/>
                    ) : null}

                    <FormErrorMessage message={validationError ?? undefined}/>
                    <FormErrorMessage message={uploadError ?? undefined}/>

                    {uploadMutation.isPending || progress > 0 ? (
                        <ResumeUploadProgress progress={progress}/>
                    ) : null}

                    {uploadedResume ? (
                        <ResumeUploadSuccess fileName={uploadedResume.fileName}/>
                    ) : null}

                    <Button
                        type="submit"
                        disabled={!selectedFile || uploadMutation.isPending || Boolean(uploadedResume)}
                        className="w-full"
                    >
                        {uploadMutation.isPending
                            ? "Uploading..."
                            : "Upload resume"}
                    </Button>
                </form>
            </CardContent>
        </Card>
    );
}
