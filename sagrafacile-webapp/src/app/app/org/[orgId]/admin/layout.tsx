'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { useParams, usePathname, useRouter } from 'next/navigation';
import { useAuth } from '@/contexts/AuthContext'; // Necessario per l'email utente nel pulsante di logout
import { useOrganization } from '@/contexts/OrganizationContext'; // Necessario per il selettore dell'organizzazione e lo stato della giornata
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"; // Importa componenti Alert
import { Sheet, SheetContent, SheetTrigger } from "@/components/ui/sheet";
import { AlertCircle, Menu } from 'lucide-react'; // Importa icone
import { AdminNavigation } from '@/components/admin/AdminNavigation';
import { InstanceProvider } from '@/contexts/InstanceContext'; // Import InstanceProvider

// Questo layout presuppone che il genitore OrganizationLayout abbia già gestito
// i controlli di autenticazione, la convalida dell'organizzazione e l'impostazione del contesto.
// Fornisce solo la shell dell'interfaccia utente specifica per l'amministratore (barra laterale, intestazione).

export default function AdminLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const params = useParams();
  const pathname = usePathname();
  const { user, logout } = useAuth(); // Ottiene l'utente per la visualizzazione dell'email e la funzione di logout
  const {
    organizations,
    selectedOrganizationId,
    isLoadingOrgs,
    isSuperAdminContext,
    setSelectedOrganizationId, // Necessario per handleOrgChange
    currentDay, // Ottiene lo stato della giornata corrente
    isLoadingDay, // Ottiene lo stato di caricamento per la giornata
  } = useOrganization();
  const router = useRouter(); // Necessario per handleOrgChange e logout

  // Re-interpreta orgId qui perché è necessario per i link e la visualizzazione dell'intestazione
  const currentOrgId = params.orgId as string;

  const handleOrgChange = (newOrgIdStr: string) => {
    if (newOrgIdStr && newOrgIdStr !== currentOrgId) {
      setSelectedOrganizationId(newOrgIdStr);
      // Conserva il percorso corrente relativo alla radice dell'organizzazione
      // Nota: siamo già dentro /admin, quindi sostituiamo in quel contesto
      const basePath = `/app/org/${newOrgIdStr}/admin`;
      // Prova a trovare la parte del percorso *dopo* /admin/
      const adminRelativePath = pathname.split(`/app/org/${currentOrgId}/admin`)[1] || '';
      const newPath = `${basePath}${adminRelativePath}`;

      console.log(`AdminLayout: SuperAdmin sta cambiando organizzazione da ${currentOrgId} a ${newOrgIdStr}. Navigazione a: ${newPath}`);
      router.push(newPath); // Usa push per la cronologia di navigazione
    }
  };

  const handleLogout = () => {
    logout();
    router.replace('/app/login'); // Reindirizza al login dopo il logout
  };

  // Ottiene il nome dell'organizzazione corrente per l'intestazione
  const currentOrgName = organizations.find(o => o.id === currentOrgId)?.name;
  const shouldShowHeader = currentOrgName || (isSuperAdminContext && organizations.length > 0);

  return (
    <InstanceProvider>
      <div className="flex h-full">
        {/* Desktop Sidebar - Hidden on mobile */}
        <aside className="hidden lg:flex w-64 bg-card text-card-foreground p-4 border-r border-border flex-col shrink-0">
          <div className="flex flex-col space-y-4 mb-6">
            <Link href={`/app/org/${currentOrgId}`} className="hover:opacity-80 transition-opacity">
              <h2 className="text-xl font-semibold">SagraFacile</h2>
              <p className="text-sm text-muted-foreground">Admin Panel</p>
            </Link>
            {currentOrgName && (
              <div className="px-3 py-2 bg-muted/50 rounded-lg">
                <p className="text-xs text-muted-foreground uppercase tracking-wider">Organizzazione</p>
                <p className="text-sm font-medium truncate">{currentOrgName}</p>
              </div>
            )}
          </div>
          <AdminNavigation currentOrgId={currentOrgId} />
          <div className="mt-auto space-y-3">
            <div className="px-3 py-2 bg-muted/30 rounded-lg">
              <p className="text-xs text-muted-foreground">Utente</p>
              <p className="text-sm font-medium truncate">
                {user?.firstName ? `${user.firstName} ${user.lastName}` : user?.email}
              </p>
            </div>
            <Button variant="outline" className="w-full" onClick={handleLogout}>
              Logout
            </Button>
          </div>
        </aside>

        {/* Mobile Navigation Sheet */}
        <Sheet open={isMobileMenuOpen} onOpenChange={setIsMobileMenuOpen}>
          <SheetTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              className="lg:hidden fixed top-4 left-4 z-50 bg-background/80 backdrop-blur-sm shadow-md"
            >
              <Menu className="h-6 w-6" />
              <span className="sr-only">Apri menu</span>
            </Button>
          </SheetTrigger>
          <SheetContent side="left" className="w-72 p-0 flex flex-col">
            <div className="flex-shrink-0 p-4 border-b">
              <Link href={`/app/org/${currentOrgId}`} className="block">
                <h2 className="text-xl font-semibold">SagraFacile</h2>
                <p className="text-sm text-muted-foreground">Admin Panel</p>
              </Link>
              {currentOrgName && (
                <div className="mt-3 px-3 py-2 bg-muted/50 rounded-lg">
                  <p className="text-xs text-muted-foreground uppercase tracking-wider">Organizzazione</p>
                  <p className="text-sm font-medium truncate">{currentOrgName}</p>
                </div>
              )}
            </div>
            <div className="flex-1 overflow-y-auto p-4">
              <AdminNavigation
                currentOrgId={currentOrgId}
                onLinkClick={() => setIsMobileMenuOpen(false)}
              />
            </div>
            <div className="flex-shrink-0 p-4 border-t bg-muted/30">
              <div className="space-y-3">
                <div className="px-3 py-2 bg-muted/50 rounded-lg">
                  <p className="text-xs text-muted-foreground">Utente</p>
                  <p className="text-sm font-medium truncate">
                    {user?.firstName ? `${user.firstName} ${user.lastName}` : user?.email}
                  </p>
                </div>
                <Button variant="outline" className="w-full" onClick={handleLogout}>
                  Logout
                </Button>
              </div>
            </div>
          </SheetContent>
        </Sheet>

        {/* Main Content Area */}
        <div className="flex-1 flex flex-col overflow-hidden">
          {/* Header - Only show if we have org name or SuperAdmin with orgs */}
          {shouldShowHeader && (
            <header className="bg-card border-b border-border p-4 flex justify-between items-center shrink-0">
              <div className="flex items-center gap-4">
                {/* Mobile menu button space */}
                <div className="lg:hidden w-10"></div>
                {currentOrgName && (
                  <div className="flex items-center gap-3">
                    <div className="w-2 h-2 bg-green-500 rounded-full"></div>
                    <div>
                      <h1 className="text-lg font-semibold truncate">{currentOrgName}</h1>
                      <p className="text-xs text-muted-foreground">Pannello Amministrazione</p>
                    </div>
                  </div>
                )}
              </div>
              {isSuperAdminContext && organizations.length > 0 && (
                <div className="flex items-center space-x-2">
                  <span className="text-sm text-muted-foreground hidden sm:inline">Cambia Organizzazione:</span>
                  <Select
                    value={selectedOrganizationId?.toString() ?? ''}
                    onValueChange={handleOrgChange}
                    disabled={isLoadingOrgs}
                  >
                    <SelectTrigger className="w-[120px] sm:w-[180px]">
                      <SelectValue placeholder="Seleziona..." />
                    </SelectTrigger>
                    <SelectContent>
                      {organizations.map((org) => (
                        <SelectItem key={org.id} value={org.id.toString()}>
                          <span className="sm:hidden">{org.name}</span>
                          <span className="hidden sm:inline">{org.name} (ID: {org.id})</span>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
            </header>
          )}

          {/* Warning Banner for No Open Day */}
          {!isLoadingDay && !currentDay && !isSuperAdminContext && (
            <div className="p-3 sm:p-4 border-b border-yellow-300 bg-yellow-50 text-yellow-800">
              <Alert>
                <AlertCircle className="h-4 w-4 text-yellow-600" />
                <AlertTitle className="text-yellow-900">Nessuna Giornata Operativa Aperta</AlertTitle>
                <AlertDescription className="text-sm">
                  Attualmente non c'è una giornata operativa aperta per questa organizzazione. Le funzionalità principali potrebbero essere limitate.
                  <Link href={`/app/org/${currentOrgId}/admin/days`} className="font-medium underline hover:text-yellow-900 ml-1">
                    Aprire una nuova giornata.
                  </Link>
                </AlertDescription>
              </Alert>
            </div>
          )}

          {/* Page Content */}
          <main className={`flex-1 overflow-y-auto ${shouldShowHeader ? 'p-4 sm:p-6' : 'p-6'} ${!isLoadingDay && !currentDay && !isSuperAdminContext ? 'pt-3' : ''}`}>
              {children}
          </main>
        </div>
      </div>
    </InstanceProvider>
  );
}
// Necessario importare useRouter
