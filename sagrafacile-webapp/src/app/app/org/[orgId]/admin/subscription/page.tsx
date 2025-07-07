'use client';

import React, { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { getOrganizationById } from '@/services/organizationService';
import { OrganizationDto } from '@/types';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import {
  AlertCircle,
  Crown,
  Calendar,
  CreditCard,
  CheckCircle,
  Zap,
  Users,
  ShoppingCart,
  TrendingUp,
  Gift,
  ArrowRight,
  Sparkles
} from 'lucide-react';

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

  const getStatusBadge = (status: string) => {
    switch (status?.toLowerCase()) {
      case 'trial':
        return (
          <Badge className="bg-blue-100 text-blue-800 border-blue-200">
            <Gift className="w-3 h-3 mr-1" />
            Prova Gratuita
          </Badge>
        );
      case 'active':
        return (
          <Badge className="bg-green-100 text-green-800 border-green-200">
            <CheckCircle className="w-3 h-3 mr-1" />
            Attivo
          </Badge>
        );
      case 'expired':
        return (
          <Badge variant="destructive">
            <AlertCircle className="w-3 h-3 mr-1" />
            Scaduto
          </Badge>
        );
      default:
        return (
          <Badge variant="outline">
            <AlertCircle className="w-3 h-3 mr-1" />
            Non specificato
          </Badge>
        );
    }
  };

  const trialFeatures = [
    { icon: ShoppingCart, text: "20 ordini al giorno", included: true },
    { icon: Users, text: "Utenti illimitati", included: true },
    { icon: TrendingUp, text: "Analytics di base", included: true },
    { icon: Zap, text: "Supporto email", included: true }
  ];

  const proFeatures = [
    { icon: ShoppingCart, text: "Ordini illimitati", included: true },
    { icon: Users, text: "Utenti illimitati", included: true },
    { icon: TrendingUp, text: "Analytics avanzate", included: true },
    { icon: Zap, text: "Supporto prioritario", included: true },
    { icon: Crown, text: "Funzionalità premium", included: true }
  ];

  if (loading) {
    return (
      <div className="space-y-6">
        <div className="space-y-2">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-4 w-96" />
        </div>
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <Skeleton className="h-64" />
          <Skeleton className="h-64" />
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header Section */}
      <div className="space-y-2">
        <h1 className="text-2xl sm:text-3xl font-bold">Gestione Sottoscrizione</h1>
        <p className="text-muted-foreground">
          Gestisci il tuo piano di sottoscrizione per{' '}
          <span className="font-medium">{organization?.name}</span>
        </p>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Errore</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {/* Current Plan Overview */}
      <Card className="border-2 border-primary/20 bg-gradient-to-br from-primary/5 to-primary/10">
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="space-y-1">
              <CardTitle className="flex items-center gap-2">
                <Crown className="w-5 h-5 text-primary" />
                Piano Attuale
              </CardTitle>
              <CardDescription>
                Il tuo piano di sottoscrizione attivo
              </CardDescription>
            </div>
            {organization && getStatusBadge(organization.subscriptionStatus || 'trial')}
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div className="flex items-center gap-3 p-3 bg-white/50 rounded-lg">
              <div className="p-2 bg-blue-100 rounded-lg">
                <Calendar className="w-4 h-4 text-blue-600" />
              </div>
              <div>
                <p className="text-sm font-medium">Tipo Piano</p>
                <p className="text-xs text-muted-foreground">
                  {organization?.subscriptionStatus?.toLowerCase() === 'trial' ? 'Prova Gratuita' : 'Piano Pro'}
                </p>
              </div>
            </div>
            <div className="flex items-center gap-3 p-3 bg-white/50 rounded-lg">
              <div className="p-2 bg-green-100 rounded-lg">
                <CheckCircle className="w-4 h-4 text-green-600" />
              </div>
              <div>
                <p className="text-sm font-medium">Stato</p>
                <p className="text-xs text-muted-foreground">
                  {organization?.subscriptionStatus?.toLowerCase() === 'trial' ? 'Attiva' : 'Attivo'}
                </p>
              </div>
            </div>
            <div className="flex items-center gap-3 p-3 bg-white/50 rounded-lg">
              <div className="p-2 bg-purple-100 rounded-lg">
                <CreditCard className="w-4 h-4 text-purple-600" />
              </div>
              <div>
                <p className="text-sm font-medium">Fatturazione</p>
                <p className="text-xs text-muted-foreground">
                  {organization?.subscriptionStatus?.toLowerCase() === 'trial' ? 'Gratuita' : 'Mensile'}
                </p>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Plans Comparison */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Trial Plan */}
        <Card className={organization?.subscriptionStatus?.toLowerCase() === 'trial' ? 'border-primary' : ''}>
          <CardHeader>
            <div className="flex items-center justify-between">
              <div className="space-y-1">
                <CardTitle className="flex items-center gap-2">
                  <Gift className="w-5 h-5 text-blue-500" />
                  Piano Prova Gratuita
                </CardTitle>
                <CardDescription>
                  Perfetto per iniziare e testare SagraFacile
                </CardDescription>
              </div>
              {organization?.subscriptionStatus?.toLowerCase() === 'trial' && (
                <Badge className="bg-blue-100 text-blue-800">Attuale</Badge>
              )}
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="text-center py-4">
              <div className="text-3xl font-bold">Gratuito</div>
              <div className="text-sm text-muted-foreground">Per sempre</div>
            </div>
            <div className="space-y-3">
              {trialFeatures.map((feature, index) => (
                <div key={index} className="flex items-center gap-3">
                  <CheckCircle className="w-4 h-4 text-green-500" />
                  <span className="text-sm">{feature.text}</span>
                </div>
              ))}
            </div>
            {organization?.subscriptionStatus?.toLowerCase() !== 'trial' && (
              <Button variant="outline" className="w-full">
                Piano Attuale
              </Button>
            )}
          </CardContent>
        </Card>

        {/* Pro Plan */}
        <Card className="border-2 border-primary relative overflow-hidden">
          <div className="absolute top-0 right-0 bg-primary text-primary-foreground px-3 py-1 text-xs font-medium">
            Consigliato
          </div>
          <CardHeader>
            <div className="space-y-1">
              <CardTitle className="flex items-center gap-2">
                <Sparkles className="w-5 h-5 text-primary" />
                Piano Pro
              </CardTitle>
              <CardDescription>
                Per eventi professionali senza limiti
              </CardDescription>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="text-center py-4">
              <div className="text-3xl font-bold">€29</div>
              <div className="text-sm text-muted-foreground">per giornata evento</div>
            </div>
            <div className="space-y-3">
              {proFeatures.map((feature, index) => (
                <div key={index} className="flex items-center gap-3">
                  <CheckCircle className="w-4 h-4 text-green-500" />
                  <span className="text-sm">{feature.text}</span>
                </div>
              ))}
            </div>
            <Button className="w-full" disabled>
              <ArrowRight className="w-4 h-4 mr-2" />
              Upgrade (Prossimamente)
            </Button>
          </CardContent>
        </Card>
      </div>

      {/* Usage Information */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <TrendingUp className="w-5 h-5 text-primary" />
            Utilizzo Attuale
          </CardTitle>
          <CardDescription>
            Monitora il tuo utilizzo del piano attuale
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            <div className="flex items-center justify-between p-4 bg-muted/50 rounded-lg">
              <div className="flex items-center gap-3">
                <ShoppingCart className="w-5 h-5 text-muted-foreground" />
                <div>
                  <p className="font-medium">Ordini Oggi</p>
                  <p className="text-sm text-muted-foreground">
                    {organization?.subscriptionStatus?.toLowerCase() === 'trial' ? 'Limite: 20 ordini/giorno' : 'Illimitati'}
                  </p>
                </div>
              </div>
              <div className="text-right">
                <p className="text-2xl font-bold">-</p>
                <p className="text-xs text-muted-foreground">In tempo reale</p>
              </div>
            </div>

            <Alert>
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Informazione</AlertTitle>
              <AlertDescription>
                Le statistiche di utilizzo dettagliate saranno disponibili nelle prossime versioni.
                Per ora puoi monitorare i tuoi ordini nella sezione Analytics.
              </AlertDescription>
            </Alert>
          </div>
        </CardContent>
      </Card>

      {/* Support Section */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Zap className="w-5 h-5 text-primary" />
            Supporto e Assistenza
          </CardTitle>
          <CardDescription>
            Hai bisogno di aiuto con la tua sottoscrizione?
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="flex items-center gap-3 p-4 border rounded-lg">
              <div className="p-2 bg-blue-100 rounded-lg">
                <AlertCircle className="w-4 h-4 text-blue-600" />
              </div>
              <div>
                <p className="font-medium">Centro Assistenza</p>
                <p className="text-sm text-muted-foreground">Guide e FAQ</p>
              </div>
            </div>
            <div className="flex items-center gap-3 p-4 border rounded-lg">
              <div className="p-2 bg-green-100 rounded-lg">
                <CreditCard className="w-4 h-4 text-green-600" />
              </div>
              <div>
                <p className="font-medium">Supporto Email</p>
                <p className="text-sm text-muted-foreground">supporto@sagrafacile.it</p>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
