'use client';

import React, { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import apiClient from '@/services/apiClient';
import { AreaResponseDto } from '@/types';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { toast } from 'sonner';
import { 
  Copy, 
  ExternalLink, 
  Globe, 
  Monitor, 
  Package, 
  CheckCircle, 
  AlertCircle,
  Loader2,
  MapPin
} from 'lucide-react';
import { useAuth } from '@/contexts/AuthContext';

export default function PublicLinksPage() {
  const { user } = useAuth();
  const params = useParams();
  const [areas, setAreas] = useState<AreaResponseDto[]>([]);
  const [isLoadingAreas, setIsLoadingAreas] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const orgId = params.orgId as string;

  const orgSlug = orgId; // orgId is already a string (Guid)


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

  // Helper component for link cards
  const LinkCard = ({ 
    title, 
    description, 
    url, 
    icon: Icon, 
    type 
  }: { 
    title: string; 
    description: string; 
    url: string; 
    icon: React.ComponentType<{ className?: string }>; 
    type: 'public' | 'staff';
  }) => (
    <Card className="group hover:shadow-md transition-shadow">
      <CardContent className="p-6">
        <div className="flex items-start gap-4">
          <div className={`p-3 rounded-lg ${type === 'public' ? 'bg-blue-100 text-blue-600' : 'bg-green-100 text-green-600'}`}>
            <Icon className="h-6 w-6" />
          </div>
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 mb-2">
              <h3 className="font-semibold text-lg">{title}</h3>
              <Badge variant={type === 'public' ? 'default' : 'secondary'} className="text-xs">
                {type === 'public' ? 'Pubblico' : 'Staff'}
              </Badge>
            </div>
            <p className="text-sm text-muted-foreground mb-4">{description}</p>
            <div className="bg-muted p-3 rounded-md mb-4">
              <code className="text-sm break-all">{url}</code>
            </div>
            <div className="flex gap-2">
              <Button 
                variant="outline" 
                size="sm" 
                onClick={() => copyToClipboard(url, title)}
                className="flex-1"
              >
                <Copy className="h-4 w-4 mr-2" />
                Copia Link
              </Button>
              <Button variant="outline" size="sm" asChild>
                <Link href={url} target="_blank" rel="noopener noreferrer">
                  <ExternalLink className="h-4 w-4 mr-2" />
                  Apri
                </Link>
              </Button>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );

  // Conditional rendering based on orgSlug availability
  if (!orgSlug) {
    return (
      <div className="space-y-6">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Link Pubblici</h1>
          <p className="text-muted-foreground">Gestisci i link pubblici per la tua organizzazione</p>
        </div>
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Slug dell'organizzazione non disponibile. Impossibile generare i link pubblici.
            Questo potrebbe essere dovuto al fatto che i dati dell'organizzazione non includono un campo 'slug'.
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  // Links can only be fully constructed if orgSlug is available
  const preOrderLink = orgSlug ? `${window.location.origin}/preorder/org/${orgSlug}` : '#';

  return (
    <div className="space-y-8">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Link Pubblici</h1>
        <p className="text-muted-foreground">Gestisci e condividi i link pubblici per la tua organizzazione</p>
      </div>

      {/* General Links Section */}
      <div className="space-y-4">
        <div className="flex items-center gap-2">
          <Globe className="h-5 w-5 text-blue-600" />
          <h2 className="text-xl font-semibold">Link Generali</h2>
        </div>
        
        <LinkCard
          title="Pagina Pre-Ordine"
          description="Permette ai clienti di effettuare pre-ordini online"
          url={preOrderLink}
          icon={Package}
          type="public"
        />
      </div>

      <Separator />

      {/* Area-Specific Links Section */}
      <div className="space-y-6">
        <div className="flex items-center gap-2">
          <MapPin className="h-5 w-5 text-green-600" />
          <h2 className="text-xl font-semibold">Link Specifici per Area</h2>
        </div>

        {isLoadingAreas && (
          <div className="space-y-4">
            <div className="flex items-center gap-2">
              <Loader2 className="h-4 w-4 animate-spin" />
              <span className="text-sm text-muted-foreground">Caricamento aree...</span>
            </div>
            {[1, 2, 3].map((i) => (
              <Card key={i}>
                <CardContent className="p-6">
                  <Skeleton className="h-6 w-48 mb-4" />
                  <Skeleton className="h-4 w-full mb-2" />
                  <Skeleton className="h-4 w-3/4" />
                </CardContent>
              </Card>
            ))}
          </div>
        )}

        {error && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {!isLoadingAreas && areas.length === 0 && !error && (
          <Alert>
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              Nessuna area trovata per questa organizzazione. Impossibile generare link specifici per area.
            </AlertDescription>
          </Alert>
        )}

        {!isLoadingAreas && areas.length > 0 && (
          <div className="space-y-8">
            {areas.map((area) => {
              const qDisplayLink = `${window.location.origin}/qdisplay/org/${orgSlug}/area/${area.id}`;
              const pickupDisplayLink = `${window.location.origin}/pickup-display/org/${orgSlug}/area/${area.id}`;
              const pickupConfirmationLink = `${window.location.origin}/app/org/${orgId}/area/${area.id}/pickup-confirmation`;

              return (
                <div key={area.id} className="space-y-4">
                  <div className="flex items-center gap-3 pb-2">
                    <div className="p-2 bg-primary/10 rounded-lg">
                      <MapPin className="h-5 w-5 text-primary" />
                    </div>
                    <div>
                      <h3 className="text-lg font-semibold">{area.name}</h3>
                      <p className="text-sm text-muted-foreground">ID: {area.id}</p>
                    </div>
                  </div>

                  <div className="grid gap-4 md:grid-cols-1 lg:grid-cols-2 xl:grid-cols-3">
                    <LinkCard
                      title="Display Coda"
                      description="Mostra lo stato della coda ai clienti"
                      url={qDisplayLink}
                      icon={Monitor}
                      type="public"
                    />
                    
                    <LinkCard
                      title="Display Ritiro Ordini"
                      description="Mostra gli ordini pronti per il ritiro"
                      url={pickupDisplayLink}
                      icon={Package}
                      type="public"
                    />
                    
                    <LinkCard
                      title="Conferma Ritiro Staff"
                      description="Interfaccia staff per confermare i ritiri"
                      url={pickupConfirmationLink}
                      icon={CheckCircle}
                      type="staff"
                    />
                  </div>

                  {areas.indexOf(area) < areas.length - 1 && <Separator />}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
