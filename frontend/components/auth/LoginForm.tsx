"use client";

import {zodResolver} from "@hookform/resolvers/zod";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {useRouter} from "next/navigation";
import {useForm} from "react-hook-form";

import {Button} from "@/components/ui/button";
import {Card, CardContent, CardHeader, CardTitle} from "@/components/ui/card";
import {Input} from "@/components/ui/input";
import {Label} from "@/components/ui/label";
import {login} from "@/lib/auth-api";
import {authQueryKeys} from "@/lib/hooks/use-auth";
import {type LoginFormValues, loginSchema,} from "@/lib/validations/auth";
import type {ApiClientError, AuthResponseDto} from "@/types/api";
import {useRedirectAuthenticatedUser} from "@/lib/hooks/use-redirect-authenticated-user";

export function LoginForm() {
    const router = useRouter();
    const queryClient = useQueryClient();
    const {shouldRender} = useRedirectAuthenticatedUser();

    const form = useForm<LoginFormValues>({
        resolver: zodResolver(loginSchema),
        defaultValues: {
            email: "",
            password: "",
        },
    });

    const loginMutation = useMutation<
        AuthResponseDto,
        ApiClientError,
        LoginFormValues
    >({
        mutationFn: login,
        onSuccess: async () => {
            await queryClient.invalidateQueries({
                queryKey: authQueryKeys.me,
            });

            router.replace("/dashboard");
        },
        onError: (error: ApiClientError) => {
            if (error.status === 401) {
                form.setError("root", {
                    message: "Invalid email or password.",
                });
                return;
            }

            form.setError("root", {
                message: error.message || "Login failed.",
            });
        },
    });

    function onSubmit(values: LoginFormValues) {
        form.clearErrors("root");
        loginMutation.mutate(values);
    }

    if (!shouldRender) {
        return null;
    }

    return (
        <Card>
            <CardHeader>
                <CardTitle>Login</CardTitle>
            </CardHeader>

            <CardContent>
                <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
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
                            autoComplete="current-password"
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
                        disabled={loginMutation.isPending}
                    >
                        {loginMutation.isPending ? "Signing in..." : "Login"}
                    </Button>
                </form>
            </CardContent>
        </Card>
    );
}