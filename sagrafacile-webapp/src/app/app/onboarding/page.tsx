'use client';

import React, { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { useAuth } from '@/contexts/AuthContext';
import organizationService from '@/services/organizationService';
import { toast } from 'sonner';
import { Loader2, CheckCircle, Building2, Users, Calendar, Utensils } from 'lucide-react';

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
      
      console.log('Organization provisioned successfully:', newOrganization);
      console.log('Current user before refresh:', user);
      
      // Refresh user context to get the new organizationId
      await refreshUser();
      
      console.log('User refreshed, redirecting to organization dashboard...');

      // Use the organization ID from the API response directly
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

  const features = [
    { icon: Users, text: "Gestione ordini e cassa" },
    { icon: Utensils, text: "Menu digitale e cucina" },
    { icon: Calendar, text: "Organizzazione eventi" },
    { icon: Building2, text: "Dashboard amministrativa" }
  ];

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-green-50 dark:from-gray-900 dark:via-gray-800 dark:to-gray-900">
      {/* Header */}
      <div className="flex items-center justify-center pt-8 pb-4">
        <div className="flex items-center space-x-3">
          <div className="w-12 h-12 bg-primary rounded-lg flex items-center justify-center">
            <Building2 className="w-6 h-6 text-primary-foreground" />
          </div>
          <div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white">SagraFacile</h1>
            <p className="text-sm text-gray-600 dark:text-gray-400">Gestionale per Eventi</p>
          </div>
        </div>
      </div>

      {/* Progress indicator */}
      <div className="flex justify-center mb-8">
        <div className="flex items-center space-x-2">
          <div className="flex items-center">
            <CheckCircle className="w-5 h-5 text-green-500" />
            <span className="ml-2 text-sm text-gray-600 dark:text-gray-400">Account creato</span>
          </div>
          <div className="w-8 h-px bg-gray-300 dark:bg-gray-600"></div>
          <div className="flex items-center">
            <div className="w-5 h-5 rounded-full bg-primary flex items-center justify-center">
              <div className="w-2 h-2 bg-white rounded-full"></div>
            </div>
            <span className="ml-2 text-sm font-medium text-gray-900 dark:text-white">Organizzazione</span>
          </div>
          <div className="w-8 h-px bg-gray-300 dark:bg-gray-600"></div>
          <div className="flex items-center">
            <div className="w-5 h-5 rounded-full border-2 border-gray-300 dark:border-gray-600"></div>
            <span className="ml-2 text-sm text-gray-400">Dashboard</span>
          </div>
        </div>
      </div>

      <div className="flex items-center justify-center px-4">
        <div className="w-full max-w-2xl">
          <div className="grid md:grid-cols-2 gap-8 items-start">
            {/* Left side - Welcome content */}
            <div className="space-y-6">
              <div>
                <h2 className="text-3xl font-bold text-gray-900 dark:text-white mb-2">
                  Benvenuto, {user?.firstName}! 👋
                </h2>
                <p className="text-lg text-gray-600 dark:text-gray-400">
                  Sei a un passo dal gestire il tuo evento in modo professionale.
                </p>
              </div>

              <div className="space-y-4">
                <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
                  Cosa potrai fare con SagraFacile:
                </h3>
                <div className="grid grid-cols-1 gap-3">
                  {features.map((feature, index) => (
                    <div key={index} className="flex items-center space-x-3 p-3 bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700">
                      <feature.icon className="w-5 h-5 text-primary" />
                      <span className="text-gray-700 dark:text-gray-300">{feature.text}</span>
                    </div>
                  ))}
                </div>
              </div>

              <div className="flex items-center space-x-2">
                <Badge variant="secondary" className="bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200">
                  Prova Gratuita
                </Badge>
                <span className="text-sm text-gray-600 dark:text-gray-400">
                  20 ordini al giorno inclusi
                </span>
              </div>
            </div>

            {/* Right side - Form */}
            <Card className="shadow-lg border-0 bg-white/80 dark:bg-gray-800/80 backdrop-blur-sm">
              <CardHeader className="text-center">
                <CardTitle className="text-xl">Crea la tua Organizzazione</CardTitle>
                <CardDescription>
                  Inizia configurando i dettagli del tuo evento o sagra
                </CardDescription>
              </CardHeader>
              <form onSubmit={handleSubmit}>
                <CardContent className="space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="organizationName" className="text-sm font-medium">
                      Nome dell'Organizzazione *
                    </Label>
                    <Input
                      id="organizationName"
                      placeholder="Es. Sagra del Pesce Fritto 2025"
                      value={organizationName}
                      onChange={(e) => setOrganizationName(e.target.value)}
                      required
                      disabled={isLoading}
                      className="h-11"
                    />
                    <p className="text-xs text-gray-500 dark:text-gray-400">
                      Questo sarà il nome principale del tuo evento
                    </p>
                  </div>
                  {error && (
                    <div className="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-md">
                      <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
                    </div>
                  )}
                </CardContent>
                <CardFooter className="flex flex-col space-y-3">
                  <Button 
                    type="submit" 
                    className="w-full h-11 text-base" 
                    disabled={isLoading || !organizationName.trim()}
                  >
                    {isLoading ? (
                      <>
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                        Creazione in corso...
                      </>
                    ) : (
                      <>
                        <Building2 className="mr-2 h-4 w-4" />
                        Crea Organizzazione
                      </>
                    )}
                  </Button>
                  <p className="text-xs text-center text-gray-500 dark:text-gray-400">
                    Potrai modificare questi dettagli in seguito
                  </p>
                </CardFooter>
              </form>
            </Card>
          </div>
        </div>
      </div>

      {/* Footer */}
      <div className="mt-16 pb-8 text-center">
        <p className="text-sm text-gray-500 dark:text-gray-400">
          Hai bisogno di aiuto? Contattaci a{' '}
          <a href="mailto:supporto@sagrafacile.it" className="text-primary hover:underline">
            supporto@sagrafacile.it
          </a>
        </p>
      </div>
    </div>
  );
}
