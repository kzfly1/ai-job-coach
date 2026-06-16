import type {ApiClientError, ApiErrorResponse} from "@/types/api";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5001";

type ApiClientOptions = RequestInit;

function isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === "object" && value != null;
}

async function parseErrorResponse(response: Response): Promise<ApiClientError> {
    const body: unknown = await response.json().catch(() => null);

    if (isRecord(body)) {
        const errorResponse: ApiErrorResponse = {
            code: typeof body.code === "string" ? body.code : "API_ERROR",
            message: typeof body.message === "string" ? body.message : "API request failed.",
            traceId: typeof body.traceId === "string" ? body.traceId : undefined,
        };

        return {
            ...errorResponse,
            status: response.status,
        };
    }

    return {
        code: "API_ERROR",
        message: "API request failed.",
        status: response.status,
    };
}

export async function apiClient<TResponse>(
    path: string,
    options: ApiClientOptions = {}
): Promise<TResponse> {
    const {headers, body, ...rest} = options;

    const isFormData = body instanceof FormData;

    const response = await fetch(buildApiUrl(path), {
        ...rest,
        body,
        credentials: "include",
        headers: {
            ...(!isFormData ? {"Content-Type": "application/json"} : {}),
            ...headers,
        },
    });

    if (!response.ok) {
        throw await parseErrorResponse(response);
    }

    if (response.status === 204) {
        return undefined as TResponse;
    }

    return (await response.json()) as TResponse;
}

export function buildApiUrl(path: string): string {
    const normalizedPath = path.startsWith("/") ? path : `/${path}`;
    return `${API_BASE_URL}${normalizedPath}`;
}
