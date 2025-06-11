'use client';

import React from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { Button } from '@/components/ui/button';

interface AdminNavigationProps {
  currentOrgId: number;
  onLinkClick?: () => void; // Callback per chiudere il menu mobile
}

export function AdminNavigation({ currentOrgId, onLinkClick }: AdminNavigationProps) {
  const pathname = usePathname();

  const handleLinkClick = () => {
    if (onLinkClick) {
      onLinkClick();
    }
  };

  return (
    <nav className="flex-1 space-y-1 overflow-y-auto">
      <div className="space-y-1">
        <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider px-3 py-2 mt-2">
          Gestione
        </h3>
        <Link href={`/app/org/${currentOrgId}/admin/users`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.includes('/admin/users') ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            Utenti
          </Button>
        </Link>
        <Link href={`/app/org/${currentOrgId}/admin/areas`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.includes('/admin/areas') ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            Aree
          </Button>
        </Link>
        <Link href={`/app/org/${currentOrgId}/admin/menu/categories`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.includes('/admin/menu/categories') ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            Categorie Menu
          </Button>
        </Link>
        <Link href={`/app/org/${currentOrgId}/admin/menu/items`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.includes('/admin/menu/items') ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            Voci di Menu
          </Button>
        </Link>
        <Link href={`/app/org/${currentOrgId}/admin/kds`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.startsWith(`/app/org/${currentOrgId}/admin/kds`) || pathname.includes('/admin/areas/') && pathname.includes('/kds') ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            Stazioni KDS
          </Button>
        </Link>
        <Link href={`/app/org/${currentOrgId}/admin/cashier-stations`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.includes('/admin/cashier-stations') ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            Postazioni Cassa
          </Button>
        </Link>
        <Link href={`/app/org/${currentOrgId}/admin/orders`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.includes('/admin/orders') ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            Ordini
          </Button>
        </Link>
        <Link href={`/app/org/${currentOrgId}/admin/days`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.includes('/admin/days') ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            Giornate
          </Button>
        </Link>
        <Link href={`/app/org/${currentOrgId}/admin/sync`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.includes('/admin/sync') ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            SagraPreOrdine
          </Button>
        </Link>
        <Link href={`/app/org/${currentOrgId}/admin/printers`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.includes('/admin/printers') ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            Stampanti
          </Button>
        </Link>
        <Link href={`/app/org/${currentOrgId}/admin/printer-assignments`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.includes('/admin/printer-assignments') ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            Assegnazione Stampanti
          </Button>
        </Link>
        <Link href={`/app/org/${currentOrgId}/admin/ads`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.includes('/admin/ads') ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            Pubblicità Display
          </Button>
        </Link>
      </div>

      <div className="space-y-1">
        <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider px-3 py-2 mt-4">
          Link Pubblici
        </h3>
        <Link href={`/app/org/${currentOrgId}/admin/public-links`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.includes('/admin/public-links') ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            Visualizza Link Pubblici
          </Button>
        </Link>
      </div>

      <div className="space-y-1">
        <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider px-3 py-2 mt-4">
          Operazioni
        </h3>
        <Link href={`/app/org/${currentOrgId}/cashier`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.includes('/cashier') ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            Cassa
          </Button>
        </Link>
        <Link href={`/app/org/${currentOrgId}/waiter`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.includes('/waiter') ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            Cameriere
          </Button>
        </Link>
        <Link href={`/app/org/${currentOrgId}/table-order`} onClick={handleLinkClick}>
          <Button 
            variant={pathname.startsWith(`/app/org/${currentOrgId}/table-order`) ? 'secondary' : 'ghost'} 
            className="w-full justify-start h-9 px-3 text-sm font-normal"
          >
            Ordini ai Tavoli
          </Button>
        </Link>
      </div>
    </nav>
  );
}
