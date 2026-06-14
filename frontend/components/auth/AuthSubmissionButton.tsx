import {Button} from "@/components/ui/button";

type AuthSubmissionButtonProps = {
    isPending: boolean;
    pendingText: string;
    children: string;
};

export function AuthSubmissionButton({isPending, pendingText, children}: AuthSubmissionButtonProps) {
    return (
        <Button type="submit" className="w-full" disabled={isPending}>
            {isPending ? pendingText : children}
        </Button>
    )
}