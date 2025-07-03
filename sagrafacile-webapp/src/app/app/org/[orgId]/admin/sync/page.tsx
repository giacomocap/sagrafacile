'use client';

import React, { useState, useEffect } from 'react';
import { useParams } from 'next/navigation';
import { useOrganization } from '@/contexts/OrganizationContext';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { toast } from 'sonner';
import { Loader2, RefreshCw } from 'lucide-react';
import { getSyncConfiguration, upsertSyncConfiguration, syncMenu } from '@/services/syncService';
import { SyncConfigurationUpsertDto, MenuSyncResult } from '@/types';

export default function SyncPage() {
  const { orgId } = useParams();
  const organizationId = orgId as string;
  // selectedOrganizationId removed as it's unused
  useOrganization(); // Call useOrganization if it has side effects or to satisfy hooks rules

  // Form state
  const [platformBaseUrl, setPlatformBaseUrl] = useState('');
  const [apiKey, setApiKey] = useState('');
  const [isEnabled, setIsEnabled] = useState(false);
  const [configId, setConfigId] = useState<number | null>(null);

  // Loading states
  const [isLoadingConfig, setIsLoadingConfig] = useState(true);
  const [isSavingConfig, setIsSavingConfig] = useState(false);
  const [isSyncing, setIsSyncing] = useState(false);

  // Validation state
  const [errors, setErrors] = useState<{
    platformBaseUrl?: string;
    apiKey?: string;
  }>({});

  // Load existing configuration
  useEffect(() => {
    const fetchConfig = async () => {
      if (!organizationId) return;
      
      setIsLoadingConfig(true);
      try {
        const config = await getSyncConfiguration(organizationId);
        if (config) {
          setPlatformBaseUrl(config.platformBaseUrl);
          setApiKey(config.apiKey);
          setIsEnabled(config.isEnabled);
          setConfigId(config.id);
        }
      } catch (error) {
        console.error('Error fetching sync configuration:', error);
        toast.error('Errore', {
          description: 'Impossibile caricare la configurazione di sincronizzazione.',
        });
      } finally {
        setIsLoadingConfig(false);
      }
    };

    fetchConfig();
  }, [organizationId]); // Removed toast from dependencies

  // Validate form
  const validateForm = (): boolean => {
    const newErrors: { platformBaseUrl?: string; apiKey?: string } = {};

    if (!platformBaseUrl.trim()) {
      newErrors.platformBaseUrl = 'URL piattaforma richiesto';
    } else if (!/^https?:\/\/[^\s/$.?#].[^\s]*$/i.test(platformBaseUrl)) {
      newErrors.platformBaseUrl = 'URL piattaforma non valido';
    }

    if (!apiKey.trim()) {
      newErrors.apiKey = 'API Key richiesta';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  // Save configuration
  const handleSaveConfig = async () => {
    if (!validateForm()) return;

    setIsSavingConfig(true);
    try {
      const configData: SyncConfigurationUpsertDto = {
        platformBaseUrl,
        apiKey,
        isEnabled,
      };

      const savedConfig = await upsertSyncConfiguration(organizationId, configData);
      setConfigId(savedConfig.id);

      toast.success('Configurazione salvata', {
        description: 'La configurazione di sincronizzazione è stata salvata con successo.',
      });
    } catch (error) {
      console.error('Error saving sync configuration:', error);
      toast.error('Errore', {
        description: 'Impossibile salvare la configurazione di sincronizzazione.',
      });
    } finally {
      setIsSavingConfig(false);
    }
  };

  // Trigger menu synchronization
  const handleSyncMenu = async () => {
    setIsSyncing(true);
    try {
      const result: MenuSyncResult = await syncMenu(organizationId);
      
      if (result.success) {
        toast.success('Sincronizzazione completata', {
          description: 'Il menu è stato sincronizzato con successo con la piattaforma Sagrafacile.',
        });
      } else {
        toast.error('Errore di sincronizzazione', {
          description: result.errorMessage || 'Si è verificato un errore durante la sincronizzazione del menu.',
        });
        
        if (result.errorDetails) {
          console.error('Dettagli errore di sincronizzazione:', result.errorDetails);
        }
      }
    } catch (error) {
      console.error('Errore durante la sincronizzazione del menu:', error);
      toast.error('Errore', {
        description: 'Impossibile sincronizzare il menu con la piattaforma Sagrafacile.',
      });
    } finally {
      setIsSyncing(false);
    }
  };

  const hasConfiguration = configId !== null;
  const canSync = hasConfiguration && isEnabled && !isLoadingConfig;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold mb-2">Integrazione Sagrafacile</h1>
        <p className="text-muted-foreground">
          Configura l'integrazione con la piattaforma Sagrafacile per consentire ai clienti di effettuare preordini online.
        </p>
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        {/* Configuration Card */}
        <Card>
          <CardHeader>
            <CardTitle>Configurazione</CardTitle>
            <CardDescription>
              Inserisci i dettagli di connessione alla piattaforma Sagrafacile.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {isLoadingConfig ? (
              <div className="flex justify-center py-4">
                <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
              </div>
            ) : (
              <>
                <div className="space-y-2">
                  <Label htmlFor="platformBaseUrl">URL Piattaforma</Label>
                  <Input
                    id="platformBaseUrl"
                    placeholder="https://tua-istanza.sagrafacile.com"
                    value={platformBaseUrl}
                    onChange={(e) => setPlatformBaseUrl(e.target.value)}
                  />
                  {errors.platformBaseUrl && (
                    <p className="text-sm text-destructive">{errors.platformBaseUrl}</p>
                  )}
                  <p className="text-sm text-muted-foreground">
                    Inserisci l'URL base della tua istanza Sagrafacile (senza slash finale).
                  </p>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="apiKey">API Key</Label>
                  <Input
                    id="apiKey"
                    type="password"
                    placeholder="api_key_xxxxx"
                    value={apiKey}
                    onChange={(e) => setApiKey(e.target.value)}
                  />
                  {errors.apiKey && (
                    <p className="text-sm text-destructive">{errors.apiKey}</p>
                  )}
                  <p className="text-sm text-muted-foreground">
                    La chiave API per l'autenticazione con la piattaforma Sagrafacile.
                  </p>
                </div>

                <div className="flex items-center space-x-2 pt-2">
                  <Switch
                    id="isEnabled"
                    checked={isEnabled}
                    onCheckedChange={setIsEnabled}
                  />
                  <Label htmlFor="isEnabled">Abilita sincronizzazione</Label>
                </div>
              </>
            )}
          </CardContent>
          <CardFooter>
            <Button
              onClick={handleSaveConfig}
              disabled={isLoadingConfig || isSavingConfig}
            >
              {isSavingConfig && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Salva Configurazione
            </Button>
          </CardFooter>
        </Card>

        {/* Sync Card */}
        <Card>
          <CardHeader>
            <CardTitle>Sincronizzazione Menu</CardTitle>
            <CardDescription>
              Sincronizza il menu attuale con la piattaforma Sagrafacile.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <p>
              Questa operazione invierà la struttura attuale del menu (Aree, Categorie, Prodotti) alla piattaforma Sagrafacile.
            </p>
            <p>
              La sincronizzazione è unidirezionale (da questo software a Sagrafacile) e sovrascriverà i dati esistenti sulla piattaforma.
            </p>
            {!hasConfiguration && !isLoadingConfig && (
              <div className="rounded-md bg-amber-50 p-4 border border-amber-200">
                <p className="text-amber-800">
                  È necessario salvare la configurazione prima di poter sincronizzare il menu.
                </p>
              </div>
            )}
            {hasConfiguration && !isEnabled && !isLoadingConfig && (
              <div className="rounded-md bg-amber-50 p-4 border border-amber-200">
                <p className="text-amber-800">
                  La sincronizzazione è disabilitata. Abilita la sincronizzazione nelle impostazioni di configurazione.
                </p>
              </div>
            )}
          </CardContent>
          <CardFooter>
            <Button
              variant="outline"
              onClick={handleSyncMenu}
              disabled={!canSync || isSyncing}
              className="w-full"
            >
              {isSyncing ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <RefreshCw className="mr-2 h-4 w-4" />
              )}
              Sincronizza Menu Ora
            </Button>
          </CardFooter>
        </Card>
      </div>
    </div>
  );
}
