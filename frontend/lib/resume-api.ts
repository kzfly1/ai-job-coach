import {apiClient, buildApiUrl} from "@/lib/api-client";
import type {ApiClientError, ApiErrorResponse, ResumeDto} from "@/types/api";

export type UploadResumeOptions = {
    file: File;
    onProgress?: (progressPercentage: number) => void;
};

function parseJson<T>(text: string): T | null {
    if (!text) {
        return null;
    }

    try {
        return JSON.parse(text) as T;
    } catch {
        return null;
    }
}

function createFallbackError(status: number): ApiClientError {
    return {
        code: "API_ERROR",
        message: "Resume request failed.",
        status,
    };
}

export function listResumes(): Promise<ResumeDto[]> {
    return apiClient<ResumeDto[]>("/api/resumes", {
        method: "GET",
    });
}

export function getResumeById(id: string): Promise<ResumeDto> {
    return apiClient<ResumeDto>(`/api/resumes/${id}`, {
        method: "GET",
    });
}

export function deleteResume(id: string): Promise<void> {
    return apiClient<void>(`/api/resumes/${id}`, {
        method: "DELETE",
    });
}

export function uploadResume({file, onProgress,}: UploadResumeOptions): Promise<ResumeDto> {
    return new Promise((resolve, reject) => {
        const formData = new FormData();
        formData.append("file", file);

        const xhr = new XMLHttpRequest();

        xhr.open("POST", buildApiUrl("/api/resumes"));
        xhr.withCredentials = true;

        xhr.upload.onprogress = (event) => {
            if (!event.lengthComputable || !onProgress) {
                return;
            }

            const progressPercentage = Math.round((event.loaded / event.total) * 100);
            onProgress(progressPercentage);
        };

        xhr.onload = () => {
            const isSuccess = xhr.status >= 200 && xhr.status < 300;

            if (isSuccess) {
                const responseBody = parseJson<ResumeDto>(xhr.responseText);

                if (responseBody) {
                    resolve(responseBody);
                    return;
                }

                reject({
                    code: "API_ERROR",
                    message: "Resume upload succeeded but returned an invalid response.",
                    status: xhr.status,
                } satisfies ApiClientError);
                return;
            }

            const errorBody = parseJson<ApiErrorResponse>(xhr.responseText);

            reject({
                ...(errorBody ?? createFallbackError(xhr.status)),
                status: xhr.status,
            } satisfies ApiClientError);
        };

        xhr.onerror = () => {
            reject({
                code: "NETWORK_ERROR",
                message: "Network error while uploading resume.",
                status: 0,
            } satisfies ApiClientError);
        };

        xhr.send(formData);
    });
}