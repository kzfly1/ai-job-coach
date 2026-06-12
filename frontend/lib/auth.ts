const TOKEN_STORAGE_KEY = "ai_job_coach_token";

export function getAccessToken():string | null{
    if (typeof window === "undefined"){
        return null;
    }

    return localStorage.getItem(TOKEN_STORAGE_KEY);
}

export function setAccessToken(token: string): void {
    localStorage.setItem(TOKEN_STORAGE_KEY, token);
}

export function clearAccessToken(): void {
    localStorage.removeItem(TOKEN_STORAGE_KEY);
}