'use client';

import React, { useEffect, useCallback } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { useAuth } from '@/contexts/AuthContext';
import { AreaDto } from '@/types';
import AreaSelector from '@/components/shared/AreaSelector';
import { Loader2 } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';

export default function SelectWaiterAreaPage() {
  const router = useRouter();
  const params = useParams();
  const { user, isLoading: isAuthLoading } = useAuth();

  const orgId = params.orgId as string;

  const handleAreaSelected = useCallback((area: AreaDto) => {
    console.log(`Waiter Area selected: ${area.name} (ID: ${area.id}) for org ${orgId}`);
    setTimeout(() => {
      router.push(`/app/org/${orgId}/waiter/area/${area.id}`);
    }, 50);
  }, [router, orgId]);

  useEffect(() => {
    if (isAuthLoading) {
      return;
    }
    if (!user) {
      router.replace('/app/login');
      return;
    }
    // Add role checks if necessary, e.g., for 'Waiter' or 'Admin'
    const canAccessWaiter = user.roles?.some(role => ['waiter', 'admin', 'superadmin'].includes(role.toLowerCase()));
    if (!canAccessWaiter) {
      console.warn("SelectWaiterAreaPage: User does not have permission. Redirecting.");
      router.replace(`/app/org/${orgId}`); // Or to a "not authorized" page
      return;
    }
  }, [user, isAuthLoading, orgId, router]);

  if (isAuthLoading) {
    return (
      <div className="flex justify-center items-center h-screen">
        <Card className="w-full max-w-md">
          <CardHeader>
            <CardTitle>Caricamento Interfaccia Cameriere</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <Skeleton className="h-8 w-3/4 mx-auto" />
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
            <Loader2 className="mx-auto h-8 w-8 animate-spin text-primary" />
            <p className="text-center text-muted-foreground">Verifica autorizzazioni...</p>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (!orgId) {
    return (
      <div className="flex justify-center items-center h-screen">
        <p>ID Organizzazione non valido.</p>
      </div>
    );
  }

  return (
    <AreaSelector
      orgId={orgId}
      onAreaSelected={handleAreaSelected}
      title="Selezione Area Cameriere"
      instructionText="Scegli l'area in cui stai operando come cameriere."
      noAreasFoundText="Nessuna area configurata per questa organizzazione. Contatta un amministratore."
      loadingText="Caricamento aree..."
      autoSelectIfOne={true}
    />
  );
}
