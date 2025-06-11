'use client';

import React from 'react';
import { useOrganization } from '@/contexts/OrganizationContext';
import { Badge } from '@/components/ui/badge'; // Using Shadcn Badge for styling
import { Skeleton } from '@/components/ui/skeleton'; // For loading state
import { AlertCircle, CheckCircle, XCircle } from 'lucide-react'; // Icons

const DayStatusIndicator: React.FC = () => {
  const { currentDay, isLoadingDay, dayError } = useOrganization();

  if (isLoadingDay) {
    return <Skeleton className="h-6 w-32" />; // Placeholder while loading
  }

  if (dayError) {
    return (
      <Badge variant="destructive" className="flex items-center gap-1">
        <AlertCircle className="h-4 w-4" />
        Errore Giornata
      </Badge>
    );
  }

  if (currentDay) {
    // Assuming DayStatus.Open is 0
    const isOpen = currentDay.status === 0; // Check against the enum value if available, otherwise use 0
    return (
      <Badge variant={isOpen ? "default" : "secondary"} className={`flex items-center gap-1 ${isOpen ? 'bg-green-600 text-white hover:bg-green-700' : ''}`}>
        {isOpen ? <CheckCircle className="h-4 w-4" /> : <XCircle className="h-4 w-4" />}
        {isOpen ? 'Giornata Aperta' : 'Giornata Chiusa'}
      </Badge>
    );
  }

  // No day open and no error
  return (
    <Badge variant="secondary" className="flex items-center gap-1">
       <XCircle className="h-4 w-4" />
      Giornata Chiusa
    </Badge>
  );
};

export default DayStatusIndicator;
