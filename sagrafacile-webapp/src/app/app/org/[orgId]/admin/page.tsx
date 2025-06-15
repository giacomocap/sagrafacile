'use client';

import React from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useAuth } from '@/contexts/AuthContext';
import { useOrganization } from '@/contexts/OrganizationContext';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { 
  Users, 
  MapPin, 
  Menu as MenuIcon, 
  ShoppingCart, 
  Monitor, 
  Printer, 
  Calendar,
  Settings,
  ExternalLink,
  Utensils,
  ChefHat,
  Wallet
} from 'lucide-react';

export default function AdminDashboardPage() {
  const { user } = useAuth();
  const { currentDay } = useOrganization();
  const params = useParams();
  const currentOrgId = parseInt(params.orgId as string, 10);

  const managementSections = [
    {
      title: 'Utenti',
      description: 'Gestisci utenti e permessi',
      icon: Users,
      href: `/app/org/${currentOrgId}/admin/users`,
      color: 'bg-blue-500'
    },
    {
      title: 'Aree',
      description: 'Configura aree operative',
      icon: MapPin,
      href: `/app/org/${currentOrgId}/admin/areas`,
      color: 'bg-green-500'
    },
    {
      title: 'Menu',
      description: 'Categorie e voci di menu',
      icon: MenuIcon,
      href: `/app/org/${currentOrgId}/admin/menu/categories`,
      color: 'bg-orange-500'
    },
    {
      title: 'Ordini',
      description: 'Visualizza storico ordini',
      icon: ShoppingCart,
      href: `/app/org/${currentOrgId}/admin/orders`,
      color: 'bg-purple-500'
    },
    {
      title: 'Stazioni KDS',
      description: 'Sistema di Visualizzazione Cucina',
      icon: Monitor,
      href: `/app/org/${currentOrgId}/admin/kds`,
      color: 'bg-red-500'
    },
    {
      title: 'Stampanti',
      description: 'Configurazione stampanti',
      icon: Printer,
      href: `/app/org/${currentOrgId}/admin/printers`,
      color: 'bg-gray-500'
    },
    {
      title: 'Giornate',
      description: 'Gestione giornate operative',
      icon: Calendar,
      href: `/app/org/${currentOrgId}/admin/days`,
      color: 'bg-indigo-500'
    },
    {
      title: 'Configurazioni',
      description: 'Impostazioni avanzate',
      icon: Settings,
      href: `/app/org/${currentOrgId}/admin/sync`,
      color: 'bg-teal-500'
    }
  ];

  const operationalSections = [
    {
      title: 'Cassa',
      description: 'Interfaccia cassa',
      icon: Wallet,
      href: `/app/org/${currentOrgId}/cashier`,
      color: 'bg-emerald-500'
    },
    {
      title: 'Cameriere',
      description: 'Gestione ordini tavoli',
      icon: Utensils,
      href: `/app/org/${currentOrgId}/waiter`,
      color: 'bg-amber-500'
    },
    {
      title: 'Ordini ai Tavoli',
      description: 'Sistema ordini mobili',
      icon: ChefHat,
      href: `/app/org/${currentOrgId}/table-order`,
      color: 'bg-rose-500'
    }
  ];

  return (
    <div className="space-y-6">
      {/* Welcome Section */}
      <div className="space-y-2">
        <h1 className="text-2xl sm:text-3xl font-bold">Pannello di Amministrazione</h1>
        <p className="text-muted-foreground">
          Benvenuto, {user?.firstName || user?.email || 'Admin'}!
        </p>
        {currentDay && (
          <div className="flex items-center gap-2">
            <Badge variant="outline" className="bg-green-50 text-green-700 border-green-200">
              <Calendar className="w-3 h-3 mr-1" />
              Giornata Aperta: {new Date(currentDay.startTime).toLocaleDateString('it-IT')}
            </Badge>
          </div>
        )}
      </div>

      {/* Management Section */}
      <div className="space-y-4">
        <h2 className="text-xl font-semibold">Gestione</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {managementSections.map((section) => (
            <Link key={section.title} href={section.href}>
              <Card className="hover:shadow-md transition-shadow cursor-pointer h-full">
                <CardHeader className="pb-3">
                  <div className="flex items-center gap-3">
                    <div className={`p-2 rounded-lg ${section.color} text-white`}>
                      <section.icon className="w-5 h-5" />
                    </div>
                    <div className="flex-1 min-w-0">
                      <CardTitle className="text-base truncate">{section.title}</CardTitle>
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="pt-0">
                  <CardDescription className="text-sm">
                    {section.description}
                  </CardDescription>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      </div>

      {/* Operations Section */}
      <div className="space-y-4">
        <h2 className="text-xl font-semibold">Operazioni</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {operationalSections.map((section) => (
            <Link key={section.title} href={section.href}>
              <Card className="hover:shadow-md transition-shadow cursor-pointer h-full">
                <CardHeader className="pb-3">
                  <div className="flex items-center gap-3">
                    <div className={`p-2 rounded-lg ${section.color} text-white`}>
                      <section.icon className="w-5 h-5" />
                    </div>
                    <div className="flex-1 min-w-0">
                      <CardTitle className="text-base truncate">{section.title}</CardTitle>
                    </div>
                    <ExternalLink className="w-4 h-4 text-muted-foreground" />
                  </div>
                </CardHeader>
                <CardContent className="pt-0">
                  <CardDescription className="text-sm">
                    {section.description}
                  </CardDescription>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      </div>

      {/* Quick Actions */}
      <div className="space-y-4">
        <h2 className="text-xl font-semibold">Azioni Rapide</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Link Pubblici</CardTitle>
              <CardDescription>
                Visualizza e condividi i link pubblici per clienti
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Link href={`/app/org/${currentOrgId}/admin/public-links`}>
                <Button variant="outline" className="w-full">
                  <ExternalLink className="w-4 h-4 mr-2" />
                  Visualizza Link
                </Button>
              </Link>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Pubblicità Display</CardTitle>
              <CardDescription>
                Gestisci contenuti pubblicitari per i display
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Link href={`/app/org/${currentOrgId}/admin/ads`}>
                <Button variant="outline" className="w-full">
                  <Monitor className="w-4 h-4 mr-2" />
                  Gestisci Pubblicità
                </Button>
              </Link>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
