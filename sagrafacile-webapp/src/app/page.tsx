'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '../contexts/AuthContext';
import { Skeleton } from '@/components/ui/skeleton'; // For loading state

export default function HomePage() {
  const { user, isLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    // Wait until authentication status is determined
    if (isLoading) {
      return;
    }

    if (!user) {
      // Not authenticated, redirect to login
      router.replace('/app/login');
    } else {
      // Authenticated, determine redirection based on role and org ID
      const isSuperAdmin = user.roles?.includes('SuperAdmin');
      const organizationId = user.organizationId; // Assuming organizationId is decoded from token

      if (isSuperAdmin) {
        // SuperAdmin: Redirect to a default org route.
        // The org layout will handle validation and selection.
        // Using '1' as a placeholder default. Consider fetching orgs
        // and redirecting to the first one if available, or a dedicated selector page.
        router.replace(`/app/org/1/admin`); // Defaulting to org 1 for now
      } else if (organizationId) {
        // Admin or other role with an organization: Redirect to their org's admin page
        // TODO: Adjust '/admin' part based on specific roles later (e.g., /cashier)
        router.replace(`/app/org/${organizationId}/admin`);
      } else {
        // Authenticated user without an organization ID or specific role mapping
        // This might be an error state or a user type not yet handled.
        // For now, redirect to login as a fallback.
        console.error("L'utente autenticato non ha un organizationId o una rotta di ruolo specifica.", user);
        router.replace('/app/login'); // Fallback
      }
    }
  }, [user, isLoading, router]);

  // Display a loading skeleton or message while determining auth state/redirecting
  return (
    <div className="flex items-center justify-center min-h-screen">
      <div className="space-y-4 p-8">
        <Skeleton className="h-8 w-[250px]" />
        <Skeleton className="h-4 w-[200px]" />
        <Skeleton className="h-4 w-[180px]" />
      </div>
    </div>
  );
}
