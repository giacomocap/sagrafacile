'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/contexts/AuthContext';
import { Loader2 } from 'lucide-react';

/**
 * Global Not Found Handler.
 *
 * This component intercepts all 404 Not Found errors. Instead of showing a
 * 404 page, it intelligently redirects the user based on their authentication
 * state, mimicking the logic from the main login page.
 *
 * - If the user is logged in, it redirects them to their organization's
 *   admin dashboard.
 * - If the user is not logged in, it redirects them to the login page.
 *
 * This provides a seamless user experience, ensuring users always land on a
 * useful page instead of a dead end.
 */
export default function NotFound() {
  const { user, isLoading: isAuthLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    // Wait until the authentication status is resolved
    if (isAuthLoading) {
      return;
    }

    if (user) {
      // User is logged in, redirect to their admin dashboard
      const organizationId = user.organizationId;
      const isSuperAdmin = user.roles?.includes('SuperAdmin');

      if (organizationId) {
        console.log(`NotFound Redirect: User is logged in. Redirecting to org ${organizationId} admin page.`);
        router.replace(`/app/org/${organizationId}/admin`);
      } else if (isSuperAdmin) {
        // SuperAdmin might not have a specific orgId, default to 1
        console.log(`NotFound Redirect: SuperAdmin detected. Redirecting to default admin page.`);
        router.replace(`/app/org/1/admin`);
      } else {
        // Fallback for logged-in users without a clear destination: send to login
        console.error("NotFound Redirect: User is logged in but cannot determine destination. Redirecting to login.");
        router.replace('/app/login');
      }
    } else {
      // User is not logged in, redirect to the login page
      console.log("NotFound Redirect: User is not logged in. Redirecting to login page.");
      router.replace('/app/login');
    }
  }, [user, isAuthLoading, router]);

  // Render a loading indicator while the redirect is being processed
  return (
    <div className="flex min-h-screen w-full items-center justify-center bg-background">
      <div className="flex flex-col items-center gap-4">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
        <p className="text-muted-foreground">Redirecting...</p>
      </div>
    </div>
  );
}
