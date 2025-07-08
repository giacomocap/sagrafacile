'use client';

import React, { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { AlertCircle, CheckCircle, Loader2, ArrowLeft } from 'lucide-react';
import Link from 'next/link';
import apiClient from '@/services/apiClient';

export default function ForgotPasswordPage() {
    const [email, setEmail] = useState('');
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState(false);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError(null);
        setIsLoading(true);

        if (!email) {
            setError("L'email è obbligatoria.");
            setIsLoading(false);
            return;
        }

        try {
            await apiClient.post('/accounts/forgot-password', { email });
            setSuccess(true);
        } catch (err: unknown) {
            console.error("Richiesta reset password fallita:", err);
            const error = err as { response?: { data?: { message?: string } }, message?: string };
            setError(error.response?.data?.message || error.message || 'Richiesta reset password fallita.');
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
                        <CardTitle className="text-2xl font-bold">Email Inviata</CardTitle>
                        <CardDescription>
                            Controlla la tua casella di posta elettronica
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <Alert>
                            <CheckCircle className="h-4 w-4" />
                            <AlertTitle>Email di reset inviata</AlertTitle>
                            <AlertDescription>
                                Se esiste un account con l'email <strong>{email}</strong>, riceverai un'email con le istruzioni per reimpostare la password.
                            </AlertDescription>
                        </Alert>
                        <div className="text-center">
                            <Link href="/app/login">
                                <Button variant="outline" className="w-full">
                                    <ArrowLeft className="mr-2 h-4 w-4" />
                                    Torna al Login
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
                    <CardTitle className="text-2xl font-bold">Password Dimenticata</CardTitle>
                    <CardDescription>
                        Inserisci la tua email per ricevere le istruzioni di reset
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
                            <Label htmlFor="email">Email</Label>
                            <Input
                                id="email"
                                type="email"
                                value={email}
                                onChange={(e) => setEmail(e.target.value)}
                                placeholder="Inserisci la tua email"
                                required
                                disabled={isLoading}
                            />
                        </div>

                        <Button type="submit" className="w-full" disabled={isLoading}>
                            {isLoading ? (
                                <>
                                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                    Invio in corso...
                                </>
                            ) : (
                                'Invia Email di Reset'
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
