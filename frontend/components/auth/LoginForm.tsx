"use client";

import {zodResolver} from "@hookform/resolvers/zod";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {useRouter} from "next/navigation";
import {useForm} from "react-hook-form";
import {login} from "@/lib/auth-api";
import {authQueryKeys} from "@/lib/hooks/use-auth";
import {type LoginFormValues, loginSchema,} from "@/lib/validations/auth";
import type {ApiClientError, AuthResponseDto} from "@/types/api";
import {useRedirectAuthenticatedUser} from "@/lib/hooks/use-redirect-authenticated-user";
import {FormErrorMessage} from "@/components/shared/FormErrorMessage";
import {AuthTextField} from "@/components/auth/AuthTextField";
import {AuthSubmissionButton} from "@/components/auth/AuthSubmissionButton";
import {AuthFormCard} from "@/components/auth/AuthFormCard";

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
        <AuthFormCard title="Login">
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                <AuthTextField
                    id="email"
                    label="Email"
                    type="email"
                    autoComplete="email"
                    errorMessage={form.formState.errors.email?.message}
                    {...form.register("email")}
                />

                <AuthTextField
                    id="password"
                    label="Password"
                    type="password"
                    autoComplete="current-password"
                    errorMessage={form.formState.errors.password?.message}
                    {...form.register("password")}
                />

                <FormErrorMessage message={form.formState.errors.root?.message}/>

                <AuthSubmissionButton
                    isPending={loginMutation.isPending}
                    pendingText="Signing in..."
                >
                    Login
                </AuthSubmissionButton>
            </form>
        </AuthFormCard>
    );
}