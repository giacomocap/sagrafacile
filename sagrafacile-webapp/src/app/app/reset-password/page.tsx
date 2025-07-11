'use client';

import React, { useState, useEffect, Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { AlertCircle, CheckCircle, Loader2, ArrowLeft } from 'lucide-react';
import Link from 'next/link';
import apiClient from '@/services/apiClient';

function ResetPasswordForm() {
    const searchParams = useSearchParams();
    const [password, setPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState(false);
    const [token, setToken] = useState<string | null>(null);
    const [email, setEmail] = useState<string | null>(null);

    useEffect(() => {
        const tokenParam = searchParams.get('token');
        const emailParam = searchParams.get('email');
        
        if (!tokenParam || !emailParam) {
            setError('Link di reset non valido. Richiedi un nuovo link di reset password.');
            return;
        }
        
        setToken(tokenParam);
        setEmail(emailParam);
    }, [searchParams]);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError(null);

        if (!password || !confirmPassword) {
            setError('Tutti i campi sono obbligatori.');
            return;
        }

        if (password !== confirmPassword) {
            setError('Le password non coincidono.');
            return;
        }

        if (password.length < 6) {
            setError('La password deve essere di almeno 6 caratteri.');
            return;
        }

        if (!token || !email) {
            setError('Parametri di reset non validi.');
            return;
        }

        setIsLoading(true);

        try {
            await apiClient.post('/accounts/reset-password', {
                email,
                token,
                newPassword: password,
                confirmPassword
            });
            setSuccess(true);
        } catch (err: unknown) {
            console.error("Reset password fallito:", err);
            const error = err as { response?: { data?: { message?: string } }, message?: string };
            setError(error.response?.data?.message || error.message || 'Reset password fallito.');
        } finally {
            setIsLoading(false);
        }
    };

    if (success) {
        return (
            <div className="min-h-screen flex items-center justify-center bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
                <Card className="w-full max-w-md">
                    <CardHeader className="text-center">
                        <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-green-100 mb-4">
                            <CheckCircle className="h-6 w-6 text-green-600" />
                        </div>
                        <CardTitle className="text-2xl font-bold">Password Reimpostata</CardTitle>
                        <CardDescription>
                            La tua password è stata aggiornata con successo
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <Alert>
                            <CheckCircle className="h-4 w-4" />
                            <AlertTitle>Successo!</AlertTitle>
                            <AlertDescription>
                                La tua password è stata reimpostata con successo. Ora puoi accedere con la nuova password.
                            </AlertDescription>
                        </Alert>
                        <div className="text-center">
                            <Link href="/app/login">
                                <Button className="w-full">
                                    Vai al Login
                                </Button>
                            </Link>
                        </div>
                    </CardContent>
                </Card>
            </div>
        );
    }

    return (
        <div className="min-h-screen flex items-center justify-center bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
            <Card className="w-full max-w-md">
                <CardHeader className="text-center">
                    <CardTitle className="text-2xl font-bold">Reimposta Password</CardTitle>
                    <CardDescription>
                        Inserisci la tua nuova password
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <form onSubmit={handleSubmit} className="space-y-4">
                        {error && (
                            <Alert variant="destructive">
                                <AlertCircle className="h-4 w-4" />
                                <AlertTitle>Errore</AlertTitle>
                                <AlertDescription>{error}</AlertDescription>
                            </Alert>
                        )}
                        
                        <div className="space-y-2">
                            <Label htmlFor="password">Nuova Password</Label>
                            <Input
                                id="password"
                                type="password"
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                placeholder="Inserisci la nuova password"
                                required
                                disabled={isLoading}
                                minLength={6}
                            />
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="confirmPassword">Conferma Password</Label>
                            <Input
                                id="confirmPassword"
                                type="password"
                                value={confirmPassword}
                                onChange={(e) => setConfirmPassword(e.target.value)}
                                placeholder="Conferma la nuova password"
                                required
                                disabled={isLoading}
                                minLength={6}
                            />
                        </div>

                        <Button type="submit" className="w-full" disabled={isLoading || !token || !email}>
                            {isLoading ? (
                                <>
                                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                    Reimpostazione...
                                </>
                            ) : (
                                'Reimposta Password'
                            )}
                        </Button>

                        <div className="text-center">
                            <Link href="/app/login">
                                <Button variant="ghost" className="w-full">
                                    <ArrowLeft className="mr-2 h-4 w-4" />
                                    Torna al Login
                                </Button>
                            </Link>
                        </div>
                    </form>
                </CardContent>
            </Card>
        </div>
    );
}

export default function ResetPasswordPage() {
    return (
        <Suspense fallback={
            <div className="min-h-screen flex items-center justify-center bg-gray-50">
                <Loader2 className="h-8 w-8 animate-spin" />
            </div>
        }>
            <ResetPasswordForm />
        </Suspense>
    );
}
