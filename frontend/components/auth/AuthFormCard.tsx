import type {ReactNode} from "react";

import {Card, CardContent, CardHeader, CardTitle} from "@/components/ui/card";

type AuthFormCardProps = {
    title: string;
    children: ReactNode;
}

export function AuthFormCard({title, children}: AuthFormCardProps) {
    return (
        <Card>
            <CardHeader>
                <CardTitle>{title}</CardTitle>
            </CardHeader>

            <CardContent>{children}</CardContent>
        </Card>
    )
}