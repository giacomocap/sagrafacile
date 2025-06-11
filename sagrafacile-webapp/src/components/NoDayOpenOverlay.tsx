'use client';

import React from 'react';
import { useOrganization } from '@/contexts/OrganizationContext';
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { AlertCircle } from 'lucide-react';

interface NoDayOpenOverlayProps {
  children: React.ReactNode;
}

const NoDayOpenOverlay: React.FC<NoDayOpenOverlayProps> = ({ children }) => {
  const { currentDay, isLoadingDay, isSuperAdminContext } = useOrganization();

  // Determine if the overlay should be shown
  // Don't show for SuperAdmin, while loading, or if a day is open
  const showOverlay = !isLoadingDay && !currentDay && !isSuperAdminContext;

  return (
    <div className="relative h-full w-full"> {/* Ensure relative positioning and full size */}
      {/* Warning Overlay */}
      {showOverlay && (
        <div className="absolute inset-0 bg-background/80 backdrop-blur-sm z-10 flex items-center justify-center p-4">
          <Alert variant="destructive" className="max-w-md">
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Nessuna Giornata Operativa Aperta</AlertTitle>
            <AlertDescription>
              Le funzionalità sono limitate. Un amministratore deve aprire una nuova giornata operativa.
            </AlertDescription>
          </Alert>
        </div>
      )}

      {/* Apply blur and disable interaction to children if overlay is shown */}
      <div className={`h-full w-full ${showOverlay ? 'filter blur-sm pointer-events-none' : ''}`}>
        {children}
      </div>
    </div>
  );
};

export default NoDayOpenOverlay;
