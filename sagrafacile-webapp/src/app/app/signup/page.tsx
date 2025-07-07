'use client';

import React, { useState, FormEvent, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import apiClient from '@/services/apiClient';
import { useInstance } from '@/contexts/InstanceContext';
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import Link from 'next/link';

export default function SignupPage() {
  const { instanceInfo, loading: instanceLoading } = useInstance();
  const router = useRouter();

  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState(''); // Added confirmPassword state
  const [termsAccepted, setTermsAccepted] = useState(false);
  const [privacyAccepted, setPrivacyAccepted] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (!instanceLoading && instanceInfo?.mode !== 'saas') {
      router.replace('/app/login');
    }
  }, [instanceInfo, instanceLoading, router]);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setIsLoading(true);
    setError(null);
    setSuccessMessage(null);

    if (!termsAccepted || !privacyAccepted) {
      setError("Devi accettare i Termini di Servizio e l'Informativa sulla Privacy.");
      setIsLoading(false);
      return;
    }

    if (password !== confirmPassword) {
      setError("Le password non corrispondono.");
      setIsLoading(false);
      return;
    }

    try {
      const response = await apiClient.post('/accounts/register', {
        firstName,
        lastName,
        email,
        password,
        confirmPassword
      });

      if (response.data && response.data.message) {
        setSuccessMessage(response.data.message);
        // Optionally redirect after a delay
        setTimeout(() => {
          router.push('/app/login');
        }, 5000);
      } else {
        setError('Registrazione fallita: Risposta non valida dal server.');
      }
    } catch (err: unknown) {
      console.error("Registration error:", err);
      let errorMsg = 'Registrazione fallita: Si è verificato un errore inatteso.';
      if (typeof err === 'object' && err !== null) {
        const errorResponse = err as { response?: { data?: { errors?: { description: string }[] } }, message?: string };
        if (errorResponse.response?.data?.errors) {
          errorMsg = errorResponse.response.data.errors.map(e => e.description).join(' ');
        } else if (errorResponse.message) {
          errorMsg = `Registrazione fallita: ${String(errorResponse.message)}`;
        }
      }
      setError(errorMsg);
    } finally {
      setIsLoading(false);
    }
  };

  if (instanceLoading || instanceInfo?.mode !== 'saas') {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <p>Loading...</p>
      </div>
    );
  }

  return (
    <div className="w-full lg:grid lg:min-h-screen lg:grid-cols-2 xl:min-h-screen">
      <div className="flex items-center justify-center py-12">
        <div className="mx-auto grid w-[400px] gap-6">
          <div className="grid gap-2 text-center">
            <h1 className="text-3xl font-bold">Registrati</h1>
            <p className="text-balance text-muted-foreground">
              Crea il tuo account SagraFacile per iniziare.
            </p>
          </div>
          {successMessage ? (
            <div className="text-center p-4 bg-green-100 text-green-800 rounded-md">
              <p>{successMessage}</p>
              <p className="mt-2 text-sm">Sarai reindirizzato alla pagina di accesso a breve.</p>
            </div>
          ) : (
            <form onSubmit={handleSubmit}>
              <div className="grid gap-4">
                <div className="grid grid-cols-2 gap-4">
                  <div className="grid gap-2">
                    <Label htmlFor="firstName">Nome</Label>
                    <Input id="firstName" placeholder="Mario" required value={firstName} onChange={(e) => setFirstName(e.target.value)} disabled={isLoading} />
                  </div>
                  <div className="grid gap-2">
                    <Label htmlFor="lastName">Cognome</Label>
                    <Input id="lastName" placeholder="Rossi" required value={lastName} onChange={(e) => setLastName(e.target.value)} disabled={isLoading} />
                  </div>
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="email">Email</Label>
                  <Input id="email" type="email" placeholder="mario.rossi@example.com" required value={email} onChange={(e) => setEmail(e.target.value)} disabled={isLoading} />
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="password">Password</Label>
                  <Input id="password" type="password" required value={password} onChange={(e) => setPassword(e.target.value)} disabled={isLoading} />
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="confirmPassword">Conferma Password</Label> {/* Added Confirm Password field */}
                  <Input id="confirmPassword" type="password" required value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} disabled={isLoading} />
                </div>
                <div className="items-top flex space-x-2">
                  <Checkbox id="terms" checked={termsAccepted} onCheckedChange={(checked) => setTermsAccepted(!!checked)} disabled={isLoading} />
                  <div className="grid gap-1.5 leading-none">
                    <label htmlFor="terms" className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70">
                      Accetto i <a href="/terms" target="_blank" className="underline">Termini di Servizio</a>
                    </label>
                  </div>
                </div>
                <div className="items-top flex space-x-2">
                  <Checkbox id="privacy" checked={privacyAccepted} onCheckedChange={(checked) => setPrivacyAccepted(!!checked)} disabled={isLoading} />
                  <div className="grid gap-1.5 leading-none">
                    <label htmlFor="privacy" className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70">
                      Accetto la <a href="/privacy" target="_blank" className="underline">Informativa sulla Privacy</a>
                    </label>
                  </div>
                </div>
                {error && (
                  <p className="text-sm font-medium text-destructive">{error}</p>
                )}
                <Button type="submit" className="w-full" disabled={isLoading || !termsAccepted || !privacyAccepted}>
                  {isLoading ? 'Creazione Account...' : 'Crea Account'}
                </Button>
                <div className="mt-4 text-center text-sm">
                  Hai già un account?{" "}
                  <Link href="/app/login" className="underline">
                    Accedi
                  </Link>
                </div>
              </div>
            </form>
          )}
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
