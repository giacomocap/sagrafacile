'use client';

import React, { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useAuth } from '@/contexts/AuthContext';
import organizationService from '@/services/organizationService';
import { toast } from 'sonner';
import { Loader2 } from 'lucide-react';

export default function OnboardingPage() {
  const [organizationName, setOrganizationName] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const router = useRouter();
  const { user, refreshUser } = useAuth();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!organizationName.trim()) {
      setError('Il nome dell\'organizzazione è obbligatorio.');
      return;
    }
    setIsLoading(true);
    setError(null);

    try {
      const newOrganization = await organizationService.provisionOrganization({ organizationName });
      toast.success(`Organizazione "${newOrganization.name}" creata con successo!`);
      
      // Refresh user context to get the new organizationId
      await refreshUser();

      // Redirect to the new organization's dashboard
      router.push(`/app/org/${newOrganization.id}/admin`);

    } catch (err: any) {
      console.error('Error provisioning organization:', err);
      const errorMessage = err.response?.data?.errors?.[0] || err.response?.data?.title || 'Creazione organizzazione fallita.';
      setError(errorMessage);
      toast.error(errorMessage);
      setIsLoading(false);
    }
  };

  // Redirect if user already has an organization
  if (user && user.organizationId) {
      router.replace(`/app/org/${user.organizationId}/admin`);
      return null;
  }

  return (
    <div className="flex items-center justify-center min-h-screen bg-gray-100 dark:bg-gray-900">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle className="text-2xl">Benvenuto in SagraFacile!</CardTitle>
          <CardDescription>Crea la tua organizzazione per iniziare.</CardDescription>
        </CardHeader>
        <form onSubmit={handleSubmit}>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="organizationName">Nome Organizzazione</Label>
              <Input
                id="organizationName"
                placeholder="Es. Sagra del Pesce Fritto 2025"
                value={organizationName}
                onChange={(e) => setOrganizationName(e.target.value)}
                required
                disabled={isLoading}
              />
            </div>
            {error && <p className="text-sm text-red-500">{error}</p>}
          </CardContent>
          <CardFooter>
            <Button type="submit" className="w-full" disabled={isLoading}>
              {isLoading && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Crea Organizzazione
            </Button>
          </CardFooter>
        </form>
      </Card>
    </div>
  );
}
