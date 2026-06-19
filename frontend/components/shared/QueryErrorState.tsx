import { Button } from "@/components/ui/button";

type QueryErrorStateProps = {
    message: string;
    onRetry?: () => void;
};

export function QueryErrorState({ message, onRetry }: QueryErrorStateProps) {
    return (
        <div className="rounded-md border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive">
            <p className="font-medium">Something went wrong</p>
            <p className="mt-1">{message}</p>

            {onRetry ? (
                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={onRetry}
                    className="mt-3"
                >
                    Try again
                </Button>
            ) : null}
        </div>
    );
}
