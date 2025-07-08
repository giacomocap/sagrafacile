'use client';

import React, { useState, FormEvent, useEffect, Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import apiClient from '@/services/apiClient';
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Loader2 } from 'lucide-react';

function ResetPasswordComponent() {
  const searchParams = useSearchParams();
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [token, setToken] = useState<string | null>(null);
  const [userId, setUserId] = useState<string | null>(null);

  useEffect(() => {
    const tokenFromUrl = searchParams.get('token');
    const userIdFromUrl = searchParams.get('userId');
    if (!tokenFromUrl || !userIdFromUrl) {
      setError("Link di reset non valido o scaduto.");
    }
    setToken(tokenFromUrl);
    setUserId(userIdFromUrl);
  }, [searchParams]);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (password !== confirmPassword) {
      setError("Le password non coincidono.");
      return;
    }
    if (!token || !userId) {
        setError("Richiesta non valida. Manca il token o l'ID utente.");
        return;
    }

    setIsLoading(true);
    setError(null);
    setMessage(null);

    try {
      const response = await apiClient.post('/accounts/reset-password', {
        userId,
        token,
        password,
        confirmPassword
      });
      setMessage(response.data.message);
    } catch (err: unknown) {
        console.error("Reset password error:", err);
        const errorResponse = err as { response?: { data?: { errors?: { description: string }[] } } };
        const firstError = errorResponse.response?.data?.errors?.[0]?.description;
        setError(firstError || "Si è verificato un errore. Il link potrebbe essere scaduto. Riprova la procedura.");
    } finally {
      setIsLoading(false);
    }
  };

  if (!token || !userId) {
      return (
          <div className="flex items-center justify-center min-h-screen bg-muted/40">
              <Card className="w-full max-w-sm">
                  <CardHeader>
                      <CardTitle className="text-2xl text-destructive">Errore</CardTitle>
                  </CardHeader>
                  <CardContent>
                      <p>{error || "Caricamento..."}</p>
                      <Button asChild className="w-full mt-4">
                          <Link href="/app/login">Torna al Login</Link>
                      </Button>
                  </CardContent>
              </Card>
          </div>
      );
  }

  return (
    <div className="flex items-center justify-center min-h-screen bg-muted/40">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-2xl">Resetta la Password</CardTitle>
          <CardDescription>
            Inserisci la tua nuova password.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="grid gap-4">
            <div className="grid gap-2">
              <Label htmlFor="password">Nuova Password</Label>
              <Input
                id="password"
                type="password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                disabled={isLoading || !!message}
              />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="confirmPassword">Conferma Nuova Password</Label>
              <Input
                id="confirmPassword"
                type="password"
                required
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                disabled={isLoading || !!message}
              />
            </div>
            {message && <p className="text-sm text-green-600">{message}</p>}
            {error && <p className="text-sm text-destructive">{error}</p>}
            
            {message ? (
                 <Button asChild className="w-full">
                    <Link href="/app/login">Vai al Login</Link>
                 </Button>
            ) : (
                <Button type="submit" className="w-full" disabled={isLoading}>
                    {isLoading ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Reset in corso...</> : 'Resetta Password'}
                </Button>
            )}
          </form>
        </CardContent>
      </Card>
    </div>
  );
}


export default function ResetPasswordPage() {
    return (
        <Suspense fallback={<div className="flex items-center justify-center min-h-screen">Caricamento...</div>}>
            <ResetPasswordComponent />
        </Suspense>
    );
}
