import type {ComponentProps} from "react";

import {FormErrorMessage} from "@/components/shared/FormErrorMessage";
import {Input} from "@/components/ui/input";
import {Label} from "@/components/ui/label";

type AuthTextFieldProps = {
    id: string;
    label: string;
    errorMessage?: string;
} & ComponentProps<typeof Input>;

export function AuthTextField({id, label, errorMessage, ...inputProps}: AuthTextFieldProps) {
    return (
        <div className="space-y-2">
            <Label htmlFor={id}>{label}</Label>
            <Input id={id} {...inputProps}/>
            <FormErrorMessage message={errorMessage}/>
        </div>
    )
}