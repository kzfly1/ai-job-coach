import {apiClient} from "@/lib/api-client";
import type {AuthResponseDto, UserProfileDto} from "@/types/api";

export type RegisterRequest = {
    email: string;
    password: string;
    fullName: string;
    headline?: string | null;
};

export type LoginRequest = {
    email: string;
    password: string;
};

export type UpdateProfileRequest = {
    fullName: string;
    headline?: string | null;
};

export function register(request: RegisterRequest): Promise<AuthResponseDto> {
    return apiClient<AuthResponseDto>("api/auth/register", {
        method: "POST",
        body: JSON.stringify(request),
    });
}

export function login(request: LoginRequest): Promise<AuthResponseDto> {
    return apiClient<AuthResponseDto>("/api/auth/login", {
        method: "POST",
        body: JSON.stringify(request),
    })
}

export function logout(): Promise<void> {
    return apiClient<void>("api/auth/logout", {
        method: "POST",
    });
}

export function getMe(): Promise<UserProfileDto> {
    return apiClient<UserProfileDto>("api/auth/me", {
        method: "GET",
    });
}

export function updateMe(
    request: UpdateProfileRequest
): Promise<UserProfileDto> {
    return apiClient<UserProfileDto>("api/auth/me", {
        method: "PUT",
        body: JSON.stringify(request),
    })
}
