"use client";

import {zodResolver} from "@hookform/resolvers/zod";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {useRouter} from "next/navigation";
import {useForm} from "react-hook-form";
import {register} from "@/lib/auth-api";
import {authQueryKeys} from "@/lib/hooks/use-auth";
import {type RegisterFormValues, registerSchema,} from "@/lib/validations/auth";
import type {ApiClientError, AuthResponseDto} from "@/types/api";
import {useRedirectAuthenticatedUser} from "@/lib/hooks/use-redirect-authenticated-user";
import {FormErrorMessage} from "@/components/shared/FormErrorMessage";
import {AuthFormCard} from "@/components/auth/AuthFormCard";
import {AuthTextField} from "@/components/auth/AuthTextField";
import {AuthSubmissionButton} from "@/components/auth/AuthSubmissionButton";

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
        <AuthFormCard title="Create account">
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                <AuthTextField
                    id="fullName"
                    label="Full name"
                    autoComplete="name"
                    errorMessage={form.formState.errors.fullName?.message}
                    {...form.register("fullName")}
                />

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
                    autoComplete="new-password"
                    errorMessage={form.formState.errors.password?.message}
                    {...form.register("password")}
                />

                <FormErrorMessage message={form.formState.errors.root?.message}/>

                <AuthSubmissionButton
                    isPending={registerMutation.isPending}
                    pendingText="Creating account..."
                >
                    Create account
                </AuthSubmissionButton>
            </form>
        </AuthFormCard>
    );
}