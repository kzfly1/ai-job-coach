"use client";

import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";

import {deleteResume, getResumeById, listResumes, uploadResume, type UploadResumeOptions} from "@/lib/resume-api";
import type {ApiClientError, ResumeDto} from "@/types/api";

export const resumeQueryKeys = {
    all: ["resumes"] as const,
    detail: (id: string) => ["resumes", id] as const,
};

export function useResumes() {
    return useQuery<ResumeDto[], ApiClientError>({
        queryKey: resumeQueryKeys.all,
        queryFn: listResumes,
        staleTime: 30_000,
    });
}

export function useResume(id: string) {
    return useQuery<ResumeDto, ApiClientError>({
        queryKey: resumeQueryKeys.detail(id),
        queryFn: () => getResumeById(id),
        enabled: Boolean(id),
    });
}

export function useUploadResume() {
    const queryClient = useQueryClient();

    return useMutation<ResumeDto, ApiClientError, UploadResumeOptions>({
        mutationFn: uploadResume,
        onSuccess: async () => {
            await queryClient.invalidateQueries({
                queryKey: resumeQueryKeys.all,
            });
        }
    });
}

export function useDeleteResume() {
    const queryClient = useQueryClient();

    return useMutation<void, ApiClientError, string>({
        mutationFn: deleteResume,
        onSuccess: async () => {
            await queryClient.invalidateQueries({
                queryKey: resumeQueryKeys.all,
            });
        }
    });
}