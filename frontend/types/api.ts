export type UserProfileDto = {
    id: string;
    email: string;
    fullName: string;
    headline: string | null;
    createdAt: string;
};

export type AuthResponseDto = {
    token: string;
};

export type ResumeDto = {
    id: string;
    userId: string;
    fileName: string;
    contentTextLength: number;
    isActive: boolean;
    uploadedAt: string;
};

export type ApiErrorResponse = {
    code: string;
    message: string;
    traceId?: string;
};

export type ApiClientError = ApiErrorResponse & {
    status: number;
};