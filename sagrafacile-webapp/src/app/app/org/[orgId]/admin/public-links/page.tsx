'use client';

import React, { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import apiClient from '@/services/apiClient'; // Changed from areaService
import { AreaResponseDto } // Assuming OrganizationDto might have slug
  from '@/types';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { toast } from 'sonner';
import { Copy, ExternalLink } from 'lucide-react';
import { useAuth } from '@/contexts/AuthContext';

export default function PublicLinksPage() {
  const { user } = useAuth();
  const params = useParams();
  const [areas, setAreas] = useState<AreaResponseDto[]>([]);
  const [isLoadingAreas, setIsLoadingAreas] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const orgId = parseInt(params.orgId as string, 10);

  const orgSlug = orgId;


  useEffect(() => {
    const fetchAreas = async () => {
      if (!user) return;
      setIsLoadingAreas(true);
      setError(null);
      try {
        const response = await apiClient.get<AreaResponseDto[]>('/Areas');
        setAreas(response.data);
      } catch (err) {
        console.error('Error fetching areas:', err);
        setError('Caricamento aree fallito.');
      } finally {
        setIsLoadingAreas(false);
      }
    };
    fetchAreas();
  }, [user]);

  const copyToClipboard = (text: string, label: string) => {
    navigator.clipboard.writeText(text)
      .then(() => {
        toast.success(`Link ${label} copiato negli appunti!`);
      })
      .catch(err => {
        console.error('Impossibile copiare il testo: ', err);
        toast.error('Impossibile copiare il link.');
      });
  };

  // Conditional rendering based on orgSlug availability
  if (!orgSlug) { // Also check isLoadingOrgs to avoid premature message
    return (
      <div>
        <h1 className="text-2xl font-semibold">Link Pubblici</h1>
        <p className="mt-4 text-red-500">
          Slug dell'organizzazione non disponibile. Impossibile generare i link pubblici.
          Questo potrebbe essere dovuto al fatto che i dati dell'organizzazione non includono un campo 'slug'.
        </p>
      </div>
    );
  }

  // Links can only be fully constructed if orgSlug is available
  const preOrderLink = orgSlug ? `${window.location.origin}/preorder/org/${orgSlug}` : '#';

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Link Pubblici</h1>

      {orgSlug && (
        <Card>
          <CardHeader>
            <CardTitle>Link Generali</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Pagina</TableHead>
                  <TableHead>Link</TableHead>
                  <TableHead className="text-right">Azioni</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                <TableRow>
                  <TableCell className="font-medium">Pagina Pre-Ordine</TableCell>
                  <TableCell>
                    <Link href={preOrderLink} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:underline break-all">
                      {preOrderLink}
                    </Link>
                  </TableCell>
                  <TableCell className="text-right space-x-2">
                    <Button variant="outline" size="sm" onClick={() => copyToClipboard(preOrderLink, 'Pagina Pre-Ordine')} disabled={!orgSlug}>
                      <Copy className="h-4 w-4 mr-2" /> Copia
                    </Button>
                    <Button variant="outline" size="sm" asChild disabled={!orgSlug}>
                      <Link href={preOrderLink} target="_blank" rel="noopener noreferrer">
                        <ExternalLink className="h-4 w-4 mr-2" /> Apri
                      </Link>
                    </Button>
                  </TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {isLoadingAreas && <div>Caricamento link specifici per area...</div>}
      {error && <div className="text-red-500">{error}</div>}

      {!isLoadingAreas && areas.length === 0 && !error && orgSlug && (
        <Card>
          <CardContent className="pt-6">
            <p>Nessuna area trovata per questa organizzazione. Impossibile generare link specifici per area.</p>
          </CardContent>
        </Card>
      )}

      {!isLoadingAreas && areas.length > 0 && orgSlug && (
        <Card>
          <CardHeader>
            <CardTitle>Link Specifici per Area</CardTitle>
          </CardHeader>
          <CardContent>
            {areas.map((area) => {
              const qDisplayLink = `${window.location.origin}/qdisplay/org/${orgSlug}/area/${area.id}`;
              const pickupDisplayLink = `${window.location.origin}/pickup-display/org/${orgSlug}/area/${area.id}`;
              const pickupConfirmationLink = `${window.location.origin}/app/org/${orgId}/area/${area.id}/pickup-confirmation`;

              return (
                <div key={area.id} className="mb-6 pb-6 border-b last:border-b-0 last:mb-0 last:pb-0">
                  <h3 className="text-lg font-medium mb-3">Area: {area.name} (ID: {area.id})</h3>
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Pagina</TableHead>
                        <TableHead>Link</TableHead>
                        <TableHead className="text-right">Azioni</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      <TableRow>
                        <TableCell className="font-medium">Display Coda</TableCell>
                        <TableCell>
                          <Link href={qDisplayLink} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:underline break-all">
                            {qDisplayLink}
                          </Link>
                        </TableCell>
                        <TableCell className="text-right space-x-2">
                          <Button variant="outline" size="sm" onClick={() => copyToClipboard(qDisplayLink, `Display Coda (${area.name})`)} disabled={!orgSlug}>
                            <Copy className="h-4 w-4 mr-2" /> Copia
                          </Button>
                          <Button variant="outline" size="sm" asChild disabled={!orgSlug}>
                            <Link href={qDisplayLink} target="_blank" rel="noopener noreferrer">
                              <ExternalLink className="h-4 w-4 mr-2" /> Apri
                            </Link>
                          </Button>
                        </TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell className="font-medium">Display Ritiro Ordini</TableCell>
                        <TableCell>
                          <Link href={pickupDisplayLink} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:underline break-all">
                            {pickupDisplayLink}
                          </Link>
                        </TableCell>
                        <TableCell className="text-right space-x-2">
                          <Button variant="outline" size="sm" onClick={() => copyToClipboard(pickupDisplayLink, `Display Ritiro Ordini (${area.name})`)} disabled={!orgSlug}>
                            <Copy className="h-4 w-4 mr-2" /> Copia
                          </Button>
                          <Button variant="outline" size="sm" asChild disabled={!orgSlug}>
                            <Link href={pickupDisplayLink} target="_blank" rel="noopener noreferrer">
                              <ExternalLink className="h-4 w-4 mr-2" /> Apri
                            </Link>
                          </Button>
                        </TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell className="font-medium">Conferma Ritiro Staff</TableCell>
                        <TableCell>
                          <Link href={pickupConfirmationLink} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:underline break-all">
                            {pickupConfirmationLink}
                          </Link>
                        </TableCell>
                        <TableCell className="text-right space-x-2">
                          <Button variant="outline" size="sm" onClick={() => copyToClipboard(pickupConfirmationLink, `Conferma Ritiro Staff (${area.name})`)} >
                            <Copy className="h-4 w-4 mr-2" /> Copia
                          </Button>
                          <Button variant="outline" size="sm" asChild>
                            <Link href={pickupConfirmationLink} target="_blank" rel="noopener noreferrer">
                              <ExternalLink className="h-4 w-4 mr-2" /> Apri
                            </Link>
                          </Button>
                        </TableCell>
                      </TableRow>
                    </TableBody>
                  </Table>
                </div>
              );
            })}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
