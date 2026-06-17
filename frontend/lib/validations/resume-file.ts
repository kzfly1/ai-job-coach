const MAX_FILE_SIZE_BYTES = 5 * 1024 * 1024;

const ALLOWED_MIME_TYPES = new Set([
    "application/pdf",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
]);

const ALLOWED_EXTENSIONS = [".pdf", ".docx"];

export function validateResumeFile(file: File): string | null {
    const fileName = file.name.toLowerCase();
    const hasAllowedExtension = ALLOWED_EXTENSIONS.some((extension) =>
        fileName.endsWith(extension)
    );

    const hasKnownMimeType = file.type.length > 0;
    const hasAllowedMimeType = ALLOWED_MIME_TYPES.has(file.type);

    if (!hasAllowedExtension || (hasKnownMimeType && !hasAllowedMimeType)) {
        return "Please upload a PDF or DOCX resume.";
    }

    if (file.size > MAX_FILE_SIZE_BYTES) {
        return "Resume file must be 5 MB or smaller.";
    }

    return null;
}

export function formatResumeFileSize(bytes: number): string {
    return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
}
