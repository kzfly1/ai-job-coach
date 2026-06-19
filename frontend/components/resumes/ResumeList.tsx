"use client";

import { useState } from "react";

import { ResumeDeleteDialog } from "@/components/resumes/ResumeDeleteDialog";
import { ResumeListEmptyState } from "@/components/resumes/ResumeListEmptyState";
import { ResumeListItem } from "@/components/resumes/ResumeListItem";
import { ResumeListSkeleton } from "@/components/resumes/ResumeListSkeleton";
import { QueryErrorState } from "@/components/shared/QueryErrorState";
import { getApiErrorMessage } from "@/lib/api-error";
import { useDeleteResume, useResumes } from "@/lib/hooks/use-resumes";
import type { ResumeDto } from "@/types/api";

export function ResumeList() {
    const [selectedResumeForDeletion, setSelectedResumeForDeletion] =
        useState<ResumeDto | null>(null);

    const resumesQuery = useResumes();
    const deleteMutation = useDeleteResume();

    function handleRequestDelete(resume: ResumeDto) {
        deleteMutation.reset();
        setSelectedResumeForDeletion(resume);
    }

    function handleOpenChange(open: boolean) {
        if (!open) {
            setSelectedResumeForDeletion(null);
            deleteMutation.reset();
        }
    }

    function handleConfirmDelete() {
        if (!selectedResumeForDeletion) {
            return;
        }

        deleteMutation.mutate(selectedResumeForDeletion.id, {
            onSuccess: () => {
                setSelectedResumeForDeletion(null);
            },
        });
    }

    if (resumesQuery.isPending) {
        return <ResumeListSkeleton />;
    }

    if (resumesQuery.isError) {
        return (
            <QueryErrorState
                message={getApiErrorMessage(
                    resumesQuery.error,
                    "We couldn't load your resumes. Please try again.",
                )}
                onRetry={() => resumesQuery.refetch()}
            />
        );
    }

    if (resumesQuery.data.length === 0) {
        return <ResumeListEmptyState />;
    }

    // Tie the pending state to the row being deleted so it only affects the selected resume.
    const isDeletingSelected =
        deleteMutation.isPending &&
        deleteMutation.variables === selectedResumeForDeletion?.id;

    const deleteErrorMessage = deleteMutation.isError
        ? getApiErrorMessage(
              deleteMutation.error,
              "We couldn't delete this resume. Please try again.",
          )
        : null;

    return (
        <>
            <div className="space-y-2">
                {resumesQuery.data.map((resume) => (
                    <ResumeListItem
                        key={resume.id}
                        resume={resume}
                        onRequestDelete={handleRequestDelete}
                    />
                ))}
            </div>

            <ResumeDeleteDialog
                resume={selectedResumeForDeletion}
                isDeleting={isDeletingSelected}
                errorMessage={deleteErrorMessage}
                onConfirm={handleConfirmDelete}
                onOpenChange={handleOpenChange}
            />
        </>
    );
}
