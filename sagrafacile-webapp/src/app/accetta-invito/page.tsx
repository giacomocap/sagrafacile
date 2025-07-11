'use client';

import React, { useState, useEffect, Suspense } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { invitationService } from '@/services/invitationService';
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { AlertCircle, Loader2 } from "lucide-react";
import Link from 'next/link';

function AcceptInvitationContent() {
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [invitationDetails, setInvitationDetails] = useState<{ email: string; organizationName: string } | null>(null);
  const [isLoadingDetails, setIsLoadingDetails] = useState(true);
  const router = useRouter();
  const searchParams = useSearchParams();

  const token = searchParams.get('token');

  useEffect(() => {
    const fetchInvitationDetails = async () => {
      if (!token) {
        setError('Token di invito mancante.');
        setIsLoadingDetails(false);
        return;
      }

      try {
        const details = await invitationService.getInvitationDetails(token);
        setInvitationDetails(details);
      } catch (err: unknown) {
        console.error("Recupero dettagli invito fallito:", err);
        const error = err as { response?: { data?: { message?: string } }, message?: string };
        setError(error.response?.data?.message || error.message || 'Invito non valido o scaduto.');
      } finally {
        setIsLoadingDetails(false);
      }
    };

    fetchInvitationDetails();
  }, [token]);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);

    if (!token) {
      setError('Token di invito mancante.');
      return;
    }

    if (password !== confirmPassword) {
      setError("Le password non coincidono.");
      return;
    }

    if (!firstName || !lastName || !password) {
      setError("Tutti i campi sono obbligatori.");
      return;
    }

    setIsLoading(true);

    try {
      await invitationService.acceptInvitation({
        token,
        firstName,
        lastName,
        password,
        confirmPassword,
      });

      // Redirect to login page with success message
      router.push('/app/login?message=Registrazione completata con successo. Puoi ora effettuare il login.');
    } catch (err: unknown) {
      console.error("Accettazione invito fallita:", err);
      const error = err as { response?: { data?: { message?: string } }, message?: string };
      setError(error.response?.data?.message || error.message || 'Accettazione invito fallita.');
    } finally {
      setIsLoading(false);
    }
  };

  if (isLoadingDetails) {
    return (
      <div className="w-full lg:grid lg:min-h-screen lg:grid-cols-2 xl:min-h-screen">
        <div className="flex items-center justify-center py-12">
          <div className="mx-auto grid w-[350px] gap-6">
            <div className="flex justify-center items-center">
              <Loader2 className="h-8 w-8 animate-spin" />
              <span className="ml-2">Caricamento...</span>
            </div>
          </div>
        </div>
        <div className="flex flex-col items-center bg-muted py-6 mt-8 lg:flex lg:items-center lg:justify-center lg:p-6 lg:mt-0">
          <img
            src="/images/sagrafacile-logo-scritte.svg"
            alt="SagraFacile"
            className="w-full max-w-sm h-auto"
          />
        </div>
      </div>
    );
  }

  if (error && !invitationDetails) {
    return (
      <div className="w-full lg:grid lg:min-h-screen lg:grid-cols-2 xl:min-h-screen">
        <div className="flex items-center justify-center py-12">
          <div className="mx-auto grid w-[350px] gap-6">
            <div className="grid gap-2 text-center">
              <h1 className="text-3xl font-bold">Invito Non Valido</h1>
              <p className="text-balance text-muted-foreground">
                L'invito non è valido o è scaduto.
              </p>
            </div>
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Errore</AlertTitle>
              <AlertDescription>{error}</AlertDescription>
            </Alert>
            <div className="text-center">
              <Link href="/app/login" className="text-sm underline">
                Torna al login
              </Link>
            </div>
          </div>
        </div>
        <div className="flex flex-col items-center bg-muted py-6 mt-8 lg:flex lg:items-center lg:justify-center lg:p-6 lg:mt-0">
          <img
            src="/images/sagrafacile-logo-scritte.svg"
            alt="SagraFacile"
            className="w-full max-w-sm h-auto"
          />
        </div>
      </div>
    );
  }

  return (
    <div className="w-full lg:grid lg:min-h-screen lg:grid-cols-2 xl:min-h-screen">
      <div className="flex items-center justify-center py-12">
        <div className="mx-auto grid w-[350px] gap-6">
          <div className="grid gap-2 text-center">
            <h1 className="text-3xl font-bold">Completa la Registrazione</h1>
            <p className="text-balance text-muted-foreground">
              Sei stato invitato a unirti a <strong>{invitationDetails?.organizationName}</strong>
            </p>
            <p className="text-sm text-muted-foreground">
              Email: {invitationDetails?.email}
            </p>
          </div>
          <form onSubmit={handleSubmit}>
            <div className="grid gap-4">
              <div className="grid gap-2">
                <Label htmlFor="firstName">Nome</Label>
                <Input
                  id="firstName"
                  type="text"
                  placeholder="Mario"
                  required
                  value={firstName}
                  onChange={(e) => setFirstName(e.target.value)}
                  disabled={isLoading}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="lastName">Cognome</Label>
                <Input
                  id="lastName"
                  type="text"
                  placeholder="Rossi"
                  required
                  value={lastName}
                  onChange={(e) => setLastName(e.target.value)}
                  disabled={isLoading}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="password">Password</Label>
                <Input
                  id="password"
                  type="password"
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  disabled={isLoading}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="confirmPassword">Conferma Password</Label>
                <Input
                  id="confirmPassword"
                  type="password"
                  required
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  disabled={isLoading}
                />
              </div>
              {error && (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertTitle>Errore</AlertTitle>
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}
              <Button type="submit" className="w-full" disabled={isLoading}>
                {isLoading ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Completamento registrazione...
                  </>
                ) : (
                  'Completa Registrazione'
                )}
              </Button>
              <div className="mt-4 text-center text-sm">
                Hai già un account?{" "}
                <Link href="/app/login" className="underline">
                  Accedi
                </Link>
              </div>
            </div>
          </form>
        </div>
      </div>
      <div className="flex flex-col items-center bg-muted py-6 mt-8 lg:flex lg:items-center lg:justify-center lg:p-6 lg:mt-0">
        <img
          src="/images/sagrafacile-logo-scritte.svg"
          alt="SagraFacile"
          className="w-full max-w-sm h-auto"
        />
      </div>
    </div>
  );
}

export default function AcceptInvitationPage() {
  return (
    <Suspense fallback={
      <div className="w-full lg:grid lg:min-h-screen lg:grid-cols-2 xl:min-h-screen">
        <div className="flex items-center justify-center py-12">
          <div className="mx-auto grid w-[350px] gap-6">
            <div className="flex justify-center items-center">
              <Loader2 className="h-8 w-8 animate-spin" />
              <span className="ml-2">Caricamento...</span>
            </div>
          </div>
        </div>
        <div className="flex flex-col items-center bg-muted py-6 mt-8 lg:flex lg:items-center lg:justify-center lg:p-6 lg:mt-0">
          <img
            src="/images/sagrafacile-logo-scritte.svg"
            alt="SagraFacile"
            className="w-full max-w-sm h-auto"
          />
        </div>
      </div>
    }>
      <AcceptInvitationContent />
    </Suspense>
  );
}
