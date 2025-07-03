'use client';

import React, { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { getOrganizationById } from '@/services/organizationService';
import { OrganizationDto } from '@/types';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { AlertCircle } from 'lucide-react';

export default function SubscriptionPage() {
  const params = useParams();
  const orgId = params.orgId as string;
  const [organization, setOrganization] = useState<OrganizationDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (orgId) {
      const fetchOrganization = async () => {
        try {
          setLoading(true);
          const data = await getOrganizationById(orgId);
          setOrganization(data);
        } catch (err) {
          setError('Impossibile caricare i dati della sottoscrizione.');
          console.error(err);
        } finally {
          setLoading(false);
        }
      };
      fetchOrganization();
    }
  }, [orgId]);

  const renderSubscriptionStatus = () => {
    if (loading) {
      return <Skeleton className="h-6 w-32" />;
    }
    if (error) {
      return <span className="text-red-500">Errore</span>;
    }
    if (organization) {
      return (
        <span className="px-3 py-1 text-sm font-medium bg-blue-100 text-blue-800 rounded-full">
          {organization.subscriptionStatus || 'Non specificato'}
        </span>
      );
    }
    return null;
  };

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Gestione Sottoscrizione</h1>
      
      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Errore</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Piano Attuale</CardTitle>
          <CardDescription>
            Dettagli sul tuo piano di sottoscrizione attuale per l'organizzazione{' '}
            {loading ? <Skeleton className="h-5 w-40 inline-block" /> : <strong>{organization?.name}</strong>}.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center justify-between p-4 border rounded-lg">
            <span className="text-muted-foreground">Stato della Sottoscrizione</span>
            {renderSubscriptionStatus()}
          </div>
          <div>
            <p className="text-sm text-muted-foreground">
              Ulteriori dettagli e opzioni di gestione verranno aggiunti qui.
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
