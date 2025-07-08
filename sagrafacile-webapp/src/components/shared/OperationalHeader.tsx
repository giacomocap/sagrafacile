'use client';

import React from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/contexts/AuthContext';
import { Button } from '@/components/ui/button';
import { ArrowLeft, LogOut, User } from 'lucide-react';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

interface OperationalHeaderProps {
  title: string;
  areaName?: string;
  orgId: string;
  role: 'cashier' | 'waiter';
  compact?: boolean; // For cashier interface where space is limited
}

const OperationalHeader: React.FC<OperationalHeaderProps> = ({
  title,
  areaName,
  orgId,
  role,
  compact = false
}) => {
  const router = useRouter();
  const { user, logout } = useAuth();

  const handleGoBack = () => {
    router.push(`/app/org/${orgId}/${role}`);
  };

  const handleLogout = () => {
    logout();
    router.replace('/app/login');
  };

  if (compact) {
    // Compact version for cashier - minimal height
    return (
      <header className="bg-card border-b border-border px-3 py-2 flex justify-between items-center shrink-0">
        <div className="flex items-center gap-2 min-w-0">
          <Button
            variant="ghost"
            size="sm"
            onClick={handleGoBack}
            className="p-1 h-7 w-7"
          >
            <ArrowLeft className="h-4 w-4" />
            <span className="sr-only">Torna alla selezione area</span>
          </Button>
          <div className="min-w-0">
            <h1 className="text-sm font-semibold truncate">{title}</h1>
            {areaName && (
              <p className="text-xs text-muted-foreground truncate">{areaName}</p>
            )}
          </div>
        </div>
        
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" className="p-1 h-7 w-7">
              <User className="h-4 w-4" />
              <span className="sr-only">Menu utente</span>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-48">
            <div className="px-2 py-1.5">
              <p className="text-xs text-muted-foreground">Utente</p>
              <p className="text-sm font-medium truncate">
                {user?.firstName ? `${user.firstName} ${user.lastName}` : user?.email}
              </p>
            </div>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={handleGoBack}>
              <ArrowLeft className="mr-2 h-4 w-4" />
              Cambia Area
            </DropdownMenuItem>
            <DropdownMenuItem onClick={handleLogout}>
              <LogOut className="mr-2 h-4 w-4" />
              Logout
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </header>
    );
  }

  // Standard version for waiter - more spacious
  return (
    <header className="bg-card border-b border-border px-4 py-3 flex justify-between items-center shrink-0">
      <div className="flex items-center gap-3 min-w-0">
        <Button
          variant="ghost"
          size="sm"
          onClick={handleGoBack}
          className="flex items-center gap-2"
        >
          <ArrowLeft className="h-4 w-4" />
          Cambia Area
        </Button>
        <div className="min-w-0">
          <h1 className="text-lg font-semibold truncate">{title}</h1>
          {areaName && (
            <p className="text-sm text-muted-foreground truncate">Area: {areaName}</p>
          )}
        </div>
      </div>
      
      <div className="flex items-center gap-2">
        <div className="text-right hidden sm:block">
          <p className="text-xs text-muted-foreground">Utente</p>
          <p className="text-sm font-medium">
            {user?.firstName ? `${user.firstName} ${user.lastName}` : user?.email}
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={handleLogout}>
          <LogOut className="h-4 w-4 mr-1" />
          Logout
        </Button>
      </div>
    </header>
  );
};

export default OperationalHeader;
