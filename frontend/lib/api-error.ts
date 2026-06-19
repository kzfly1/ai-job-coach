function isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === "object" && value !== null;
}

export function getApiErrorMessage(error: unknown, fallbackMessage: string): string {
    if (isRecord(error) && typeof error.message === "string") {
        const message = error.message.trim();

        if (message.length > 0) {
            return message;
        }
    }
    return fallbackMessage;
}

export function getApiErrorStatus(error: unknown): number | null {
    if (isRecord(error) && typeof error.status === "number") {
        return error.status;
    }
    return null;
}

export function isUnauthorizedError(error: unknown): boolean {
    return getApiErrorStatus(error) === 401;
}