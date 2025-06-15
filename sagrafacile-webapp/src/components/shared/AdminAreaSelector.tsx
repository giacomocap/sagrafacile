'use client';

import React, { useState, useEffect } from 'react';
import apiClient from '@/services/apiClient';
import { AreaDto } from '@/types';
import { Card, CardHeader, CardTitle, CardContent, CardDescription } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';

interface AdminAreaSelectorProps {
  selectedAreaId: string | undefined;
  onAreaChange: (areaId: string | undefined) => void;
  title?: string;
  description?: string;
}

export default function AdminAreaSelector({
  selectedAreaId,
  onAreaChange,
  title = "Seleziona Area",
  description = "Scegli un'area per gestire il suo contenuto."
}: AdminAreaSelectorProps) {
  const [areas, setAreas] = useState<AreaDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchAreas = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const response = await apiClient.get<AreaDto[]>('/Areas');
        // The API returns only active areas, so no need to filter client-side for now.
        const fetchedAreas = response.data;
        setAreas(fetchedAreas);
        if (fetchedAreas.length === 1 && !selectedAreaId) {
          onAreaChange(fetchedAreas[0].id.toString());
        }
      } catch (err) {
        console.error('Errore nel recupero delle aree:', err);
        setError('Caricamento aree fallito.');
      } finally {
        setIsLoading(false);
      }
    };

    fetchAreas();
    // The dependency array is empty because we want this to run once on mount.
    // The parent component controls re-renders via props if needed.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleValueChange = (value: string) => {
    // The 'Select' component calls onValueChange with an empty string if the placeholder is re-selected.
    // We handle this by passing `undefined` to our callback.
    onAreaChange(value || undefined);
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <Skeleton className="h-10 w-[280px]" />
        ) : error ? (
          <p className="text-red-500">{error}</p>
        ) : areas.length > 0 ? (
          <Select onValueChange={handleValueChange} value={selectedAreaId || ''}>
            <SelectTrigger className="w-full md:w-[280px]">
              <SelectValue placeholder="Seleziona un'area" />
            </SelectTrigger>
            <SelectContent>
              {areas.map((area) => (
                <SelectItem key={area.id} value={area.id.toString()}>
                  {area.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        ) : (
          <p>Nessuna area attiva trovata. Per favore, creane una nella sezione di amministrazione.</p>
        )}
      </CardContent>
    </Card>
  );
}
