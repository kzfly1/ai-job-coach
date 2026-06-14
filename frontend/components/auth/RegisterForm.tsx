"use client";

import {zodResolver} from "@hookform/resolvers/zod";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {useRouter} from "next/navigation";
import {useForm} from "react-hook-form";

import {Button} from "@/components/ui/button";
import {Card, CardContent, CardHeader, CardTitle} from "@/components/ui/card";
import {Input} from "@/components/ui/input";
import {Label} from "@/components/ui/label";
import {register} from "@/lib/auth-api";
import {authQueryKeys} from "@/lib/hooks/use-auth";
import {type RegisterFormValues, registerSchema,} from "@/lib/validations/auth";
import type {ApiClientError, AuthResponseDto} from "@/types/api";
import {useRedirectAuthenticatedUser} from "@/lib/hooks/use-redirect-authenticated-user";

export function RegisterForm() {
    const router = useRouter();
    const queryClient = useQueryClient();
    const {shouldRender} = useRedirectAuthenticatedUser();

    const form = useForm<RegisterFormValues>({
        resolver: zodResolver(registerSchema),
        defaultValues: {
            fullName: "",
            email: "",
            password: "",
        },
    });

    const registerMutation = useMutation<
        AuthResponseDto,
        ApiClientError,
        RegisterFormValues
    >({
        mutationFn: register,
        onSuccess: async () => {
            await queryClient.invalidateQueries({
                queryKey: authQueryKeys.me,
            });

            router.replace("/dashboard");
        },
        onError: (error) => {
            if (error.status === 409) {
                form.setError("email", {
                    message: "An account with this email already exists.",
                });
                return;
            }

            form.setError("root", {
                message: error.message || "Registration failed.",
            });
        },
    });

    function onSubmit(values: RegisterFormValues) {
        form.clearErrors("root");
        form.clearErrors("email");
        registerMutation.mutate(values);
    }

    if (!shouldRender) {
        return null;
    }

    return (
        <Card>
            <CardHeader>
                <CardTitle>Create account</CardTitle>
            </CardHeader>

            <CardContent>
                <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                    <div className="space-y-2">
                        <Label htmlFor="fullName">Full name</Label>
                        <Input
                            id="fullName"
                            autoComplete="name"
                            {...form.register("fullName")}
                        />
                        {form.formState.errors.fullName ? (
                            <p className="text-sm text-destructive">
                                {form.formState.errors.fullName.message}
                            </p>
                        ) : null}
                    </div>

                    <div className="space-y-2">
                        <Label htmlFor="email">Email</Label>
                        <Input
                            id="email"
                            type="email"
                            autoComplete="email"
                            {...form.register("email")}
                        />
                        {form.formState.errors.email ? (
                            <p className="text-sm text-destructive">
                                {form.formState.errors.email.message}
                            </p>
                        ) : null}
                    </div>

                    <div className="space-y-2">
                        <Label htmlFor="password">Password</Label>
                        <Input
                            id="password"
                            type="password"
                            autoComplete="new-password"
                            {...form.register("password")}
                        />
                        {form.formState.errors.password ? (
                            <p className="text-sm text-destructive">
                                {form.formState.errors.password.message}
                            </p>
                        ) : null}
                    </div>

                    {form.formState.errors.root ? (
                        <p className="text-sm text-destructive">
                            {form.formState.errors.root.message}
                        </p>
                    ) : null}

                    <Button
                        type="submit"
                        className="w-full"
                        disabled={registerMutation.isPending}
                    >
                        {registerMutation.isPending ? "Creating account..." : "Create account"}
                    </Button>
                </form>
            </CardContent>
        </Card>
    );
}