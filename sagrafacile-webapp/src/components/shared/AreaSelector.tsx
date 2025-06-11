'use client';

import React, { useEffect, useState, useCallback } from 'react';
import apiClient from '@/services/apiClient';
import { AreaDto } from '@/types';
import { Button } from '@/components/ui/button'; // Removed ButtonProps
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { toast } from 'sonner';
import { AlertCircle, Loader2 } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';

interface AreaSelectorProps {
  orgId: string;
  onAreaSelected: (area: AreaDto) => void;
  title?: string;
  instructionText?: string;
  noAreasFoundText?: string;
  loadingText?: string;
  autoSelectIfOne?: boolean;
  buttonVariant?: React.ComponentProps<typeof Button>['variant']; // More robust way to get variant type
  buttonSize?: React.ComponentProps<typeof Button>['size']; // More robust way to get size type
  className?: string;
  cardClassName?: string;
}

export default function AreaSelector({
  orgId,
  onAreaSelected,
  title = "Selezione Area",
  instructionText = "Scegli l'area operativa.",
  noAreasFoundText = "Nessuna area trovata per questa organizzazione. Contatta un amministratore.",
  loadingText = "Caricamento aree...",
  autoSelectIfOne = true,
  buttonVariant = 'outline',
  buttonSize = 'lg',
  className = "flex justify-center items-center h-screen bg-gray-100 dark:bg-gray-900 p-4",
  cardClassName = "w-full max-w-md shadow-lg",
}: AreaSelectorProps) {
  const [areas, setAreas] = useState<AreaDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const internalHandleSelectArea = useCallback((area: AreaDto) => {
    onAreaSelected(area);
  }, [onAreaSelected]);

  useEffect(() => {
    if (!orgId) {
      setError("ID Organizzazione non fornito.");
      setIsLoading(false);
      return;
    }

    const fetchAreas = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const response = await apiClient.get<AreaDto[]>(`/Areas?organizationId=${orgId}`);
        setAreas(response.data);

        if (autoSelectIfOne && response.data.length === 1) {
          internalHandleSelectArea(response.data[0]);
        } else if (response.data.length === 0) {
          setError(noAreasFoundText);
        }
      } catch (err: any) {
        console.error("Error fetching areas:", err);
        setError("Impossibile caricare le aree.");
        toast.error("Errore nel caricamento delle aree.");
      } finally {
        setIsLoading(false);
      }
    };

    fetchAreas();
  }, [orgId, autoSelectIfOne, internalHandleSelectArea, noAreasFoundText]);

  if (isLoading) {
    return (
      <div className={className}>
        <Card className={cardClassName}>
          <CardHeader>
            <CardTitle className="text-center text-2xl font-semibold">{title}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-center text-muted-foreground">{loadingText}</p>
            <Skeleton className="h-8 w-3/4 mx-auto" />
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
          </CardContent>
        </Card>
      </div>
    );
  }

  if (error) {
    return (
      <div className={className}>
        <Alert variant="destructive" className="max-w-md">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Errore</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      </div>
    );
  }

  // Only render selection if areas > 1 (0 and 1 are handled by useEffect if autoSelectIfOne is true)
  // Or if autoSelectIfOne is false and areas.length > 0
  if (areas.length > 1 || (!autoSelectIfOne && areas.length > 0)) {
    return (
      <div className={className}>
        <Card className={cardClassName}>
          <CardHeader>
            <CardTitle className="text-center text-2xl font-semibold">{title}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-center text-muted-foreground">{instructionText}</p>
            {areas.map((area) => (
              <Button
                key={area.id}
                onClick={() => internalHandleSelectArea(area)}
                className="w-full h-12 text-lg"
                variant={buttonVariant}
                size={buttonSize}
              >
                {area.name}
              </Button>
            ))}
          </CardContent>
        </Card>
      </div>
    );
  }

  // Fallback case (e.g., if autoSelectIfOne is true and there's 1 area, this won't be reached often)
  // Or if no areas and autoSelectIfOne is false (error state handles no areas if autoSelectIfOne is true)
  return (
    <div className="flex justify-center items-center h-screen">
      <Loader2 className="h-8 w-8 animate-spin" />
      <p className="ml-2">Caricamento o elaborazione stato...</p>
    </div>
  );
}
