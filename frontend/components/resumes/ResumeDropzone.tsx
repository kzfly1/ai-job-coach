import {type ChangeEventHandler, type DragEventHandler, useRef, useState,} from "react";

import {Button} from "@/components/ui/button";

type ResumeDropzoneProps = {
    onFileSelected: (file: File) => void;
};

export function ResumeDropzone({onFileSelected}: ResumeDropzoneProps) {
    const inputRef = useRef<HTMLInputElement | null>(null);
    const [isDragging, setIsDragging] = useState(false);

    const handleInputChange: ChangeEventHandler<HTMLInputElement> = (event) => {
        const file = event.target.files?.[0];

        if (file) {
            onFileSelected(file);
        }

        event.target.value = "";
    };

    const handleDragOver: DragEventHandler<HTMLDivElement> = (event) => {
        event.preventDefault();
        setIsDragging(true);
    };

    const handleDragLeave: DragEventHandler<HTMLDivElement> = () => {
        setIsDragging(false);
    };

    const handleDrop: DragEventHandler<HTMLDivElement> = (event) => {
        event.preventDefault();
        setIsDragging(false);

        const file = event.dataTransfer.files[0];

        if (file) {
            onFileSelected(file);
        }
    };

    return (
        <div
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            className={`rounded-lg border border-dashed p-8 text-center transition ${
                isDragging ? "bg-muted" : "bg-background"
            }`}
        >
            <input
                ref={inputRef}
                type="file"
                accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                className="hidden"
                onChange={handleInputChange}
            />

            <div className="space-y-3">
                <p className="text-sm font-medium">
                    Drag and drop your resume here
                </p>

                <p className="text-sm text-muted-foreground">
                    PDF or DOCX only. Maximum file size: 5 MB.
                </p>

                <Button
                    type="button"
                    variant="outline"
                    onClick={() => inputRef.current?.click()}
                >
                    Choose file
                </Button>
            </div>
        </div>
    );
}
