type FormErrorMessageProps = {
    message?: string;
};

export function FormErrorMessage({message}: FormErrorMessageProps) {
    if (!message) {
        return null;
    }

    return <p className="text-sm text-destructive">{message}</p>
}