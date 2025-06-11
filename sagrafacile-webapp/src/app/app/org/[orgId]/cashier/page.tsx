'use client';

import React, { useEffect, useCallback } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { useAuth } from '@/contexts/AuthContext';
import { AreaDto } from '@/types';
import AreaSelector from '@/components/shared/AreaSelector'; // Import the new component
import { Loader2 } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';


export default function SelectCashierAreaPage() { // Renamed component for clarity
  const router = useRouter();
  const params = useParams();
  const { user, isLoading: isAuthLoading } = useAuth();

  const orgId = params.orgId as string;

  const handleAreaSelected = useCallback((area: AreaDto) => {
    console.log(`Cashier Area selected: ${area.name} (ID: ${area.id}) for org ${orgId}`);
    // Using a small timeout to ensure any state updates from AreaSelector complete
    // before navigation, though likely not strictly necessary here.
    setTimeout(() => {
      router.push(`/app/org/${orgId}/cashier/area/${area.id}`);
    }, 50);
  }, [router, orgId]);

  useEffect(() => {
    if (isAuthLoading) {
      return; // Wait for auth context to load
    }

    if (!user) {
      router.replace('/app/login'); // Should be handled by layout, but safeguard
      return;
    }

    // Role checks can be added here or in a layout if needed
    // For example, to ensure the user has a 'Cashier' role.
    // const isCashier = user.roles?.some(role => role.toLowerCase().includes('cashier'));
    // if (!isCashier) {
    //   console.warn("SelectCashierAreaPage: User is not a cashier. Redirecting to org dashboard or admin.");
    //   router.replace(`/app/org/${orgId}`); // Or to a specific admin page
    //   return;
    // }

  }, [user, isAuthLoading, orgId, router]);


  if (isAuthLoading) {
    // Show a generic loading state while auth is resolving
    // AreaSelector has its own loading state for fetching areas
    return (
      <div className="flex justify-center items-center h-screen">
        <Card className="w-full max-w-md">
          <CardHeader>
            <CardTitle>Caricamento Cassa</CardTitle>
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
    // This case should ideally be caught by routing or layout checks
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
      title="Selezione Area Cassa"
      instructionText="Scegli l'area cassa in cui stai operando."
      noAreasFoundText="Nessuna area di cassa configurata per questa organizzazione. Contatta un amministratore."
      loadingText="Caricamento aree cassa..."
      autoSelectIfOne={true} // Automatically redirects if only one area
    />
  );
}
