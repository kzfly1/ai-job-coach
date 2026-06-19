import {
    AlertDialog,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import type { ResumeDto } from "@/types/api";

type ResumeDeleteDialogProps = {
    resume: ResumeDto | null;
    isDeleting: boolean;
    errorMessage: string | null;
    onConfirm: () => void;
    onOpenChange: (open: boolean) => void;
};

export function ResumeDeleteDialog({
    resume,
    isDeleting,
    errorMessage,
    onConfirm,
    onOpenChange,
}: ResumeDeleteDialogProps) {
    return (
        <AlertDialog
            open={resume !== null}
            onOpenChange={(open) => {
                // Ignore dismiss attempts (Esc / overlay / Cancel) while a delete is in flight.
                if (isDeleting) {
                    return;
                }

                onOpenChange(open);
            }}
        >
            <AlertDialogContent>
                <AlertDialogHeader>
                    <AlertDialogTitle>Delete resume?</AlertDialogTitle>
                    <AlertDialogDescription>
                        This will permanently delete{" "}
                        <span className="font-medium text-foreground">
                            {resume?.fileName}
                        </span>
                        . This action cannot be undone.
                    </AlertDialogDescription>
                </AlertDialogHeader>

                {errorMessage ? (
                    <p className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
                        {errorMessage}
                    </p>
                ) : null}

                <AlertDialogFooter>
                    <AlertDialogCancel disabled={isDeleting}>Cancel</AlertDialogCancel>

                    <Button
                        type="button"
                        variant="destructive"
                        onClick={onConfirm}
                        disabled={isDeleting}
                    >
                        {isDeleting ? "Deleting..." : "Delete"}
                    </Button>
                </AlertDialogFooter>
            </AlertDialogContent>
        </AlertDialog>
    );
}
