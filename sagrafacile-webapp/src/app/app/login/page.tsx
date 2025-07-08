/* eslint-disable @next/next/no-img-element */
'use client';

import React, { useState, FormEvent, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/contexts/AuthContext';
import { useInstance } from '@/contexts/InstanceContext';
import apiClient from '@/services/apiClient';
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import Link from 'next/link';
// import { cn } from "@/lib/utils"; // cn is not used

export default function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const { login, user, isLoading: isAuthLoading } = useAuth();
  const { instanceInfo, loading: instanceLoading } = useInstance();
  const router = useRouter();

  useEffect(() => {
    // Wait for both auth and instance info to be loaded
    if (isAuthLoading || instanceLoading) {
      return;
    }

    if (user) {
      console.log("Login Page Effect: User detected, determining redirect...", { user, instanceInfo });
      const isSuperAdmin = user.roles?.includes('SuperAdmin');
      const isCashier = user.roles?.includes('Cashier');
      const organizationId = user.organizationId; // This is now a string (Guid)

      // Handle new SaaS user who needs to go through onboarding
      if (instanceInfo?.mode === 'saas' && !organizationId && !isSuperAdmin) {
        console.log("Login Page Effect: Redirecting new SaaS user to onboarding.");
        router.replace('/app/onboarding');
        return;
      }

      // Handle regular user with an organization
      if (organizationId) {
        if (isCashier) {
          console.log(`Login Page Effect: Redirecting Cashier for Org ${organizationId} to select area...`);
          router.replace(`/app/org/${organizationId}/cashier`);
        } else if (isSuperAdmin) {
          // A SuperAdmin might have an associated org, but for simplicity/consistency, we can send them to a default place.
          // Or, we could respect their orgId if it exists. Let's stick to the existing logic for now.
          console.log("Login Page Effect: Redirecting SuperAdmin to default org admin...");
          router.replace(`/app/org/1/admin`);
        } else {
          console.log(`Login Page Effect: Redirecting Admin/Other User for Org ${organizationId} to admin...`);
          router.replace(`/app/org/${organizationId}/admin`);
        }
      } else if (isSuperAdmin) { // SuperAdmin without an organizationId
        console.log("Login Page Effect: Redirecting SuperAdmin (no specific orgId) to default org admin...");
        router.replace(`/app/org/1/admin`);
      } else {
        // This path is now an explicit error state (e.g., non-SaaS user with no org, which shouldn't happen)
        console.error("Login Page Effect: User exists but cannot determine redirect path (OrgID missing or invalid?).", user);
        setError("Logged in, but could not determine your destination. Please contact support.");
      }
    }
  }, [user, isAuthLoading, router, instanceInfo, instanceLoading]);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setIsLoading(true);
    setError(null);

    try {
      const response = await apiClient.post('/accounts/login', {
        email,
        password,
      });

      // The entire response.data should be the TokenResponseDto
      const tokenResponse = response.data;

      if (tokenResponse && tokenResponse.accessToken) {
        login(tokenResponse); // Pass the whole DTO
      } else {
        setError('Login failed: Invalid response from server.');
      }
    } catch (err: unknown) {
      console.error("Login error:", err);
      let errorMsg = 'Login failed: An unexpected error occurred.';
      if (typeof err === 'object' && err !== null) {
        const errorResponse = err as { response?: { status?: number, data?: { message?: string } }, message?: string };
        if (errorResponse.response?.status === 401) {
          errorMsg = 'Login failed: Invalid email or password.';
        } else if (errorResponse.response?.data?.message) {
          errorMsg = `Login failed: ${String(errorResponse.response.data.message)}`;
        } else if (errorResponse.message) {
          errorMsg = `Login failed: ${String(errorResponse.message)}`;
        }
      }
      setError(errorMsg);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="w-full lg:grid lg:min-h-screen lg:grid-cols-2 xl:min-h-screen">
      <div className="flex items-center justify-center py-12">
        <div className="mx-auto grid w-[350px] gap-6">
          <div className="grid gap-2 text-center">
            <h1 className="text-3xl font-bold">Accedi</h1>
            <p className="text-balance text-muted-foreground">
              Inserisci la tua email per accedere al tuo account SagraFacile
            </p>
          </div>
          <form onSubmit={handleSubmit}>
            <div className="grid gap-4">
              <div className="grid gap-2">
                <Label htmlFor="email">Email</Label>
                <Input
                  id="email"
                  type="email"
                  placeholder="m@sagrafacile.it"
                  required
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  disabled={isLoading}
                />
              </div>
              <div className="grid gap-2">
                <div className="flex items-center">
                  <Label htmlFor="password">Password</Label>
                  <Link
                    href="/app/forgot-password"
                    className="ml-auto inline-block text-sm underline"
                  >
                    Password dimenticata?
                  </Link>
                </div>
                <Input
                  id="password"
                  type="password"
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  disabled={isLoading}
                />
              </div>
              {error && (
                <p className="text-sm font-medium text-destructive">{error}</p>
              )}
              <Button type="submit" className="w-full" disabled={isLoading}>
                {isLoading ? 'Accesso in corso...' : 'Accedi'}
              </Button>
              {!instanceLoading && instanceInfo?.mode === 'saas' && (
                <div className="mt-4 text-center text-sm">
                  Non hai un account?{" "}
                  <Link href="/app/signup" className="underline">
                    Registrati
                  </Link>
                </div>
              )}
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
