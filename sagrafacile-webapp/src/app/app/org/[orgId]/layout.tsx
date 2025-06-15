'use client';

import React, { useEffect, useState } from 'react';
import { useParams, useRouter, usePathname } from 'next/navigation';
// Removed Link, Select, Button imports as UI is moved
import { useAuth } from '@/contexts/AuthContext';
import { useOrganization } from '@/contexts/OrganizationContext';
import { Skeleton } from '@/components/ui/skeleton';

export default function OrganizationLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const params = useParams();
  const pathname = usePathname(); // Get the full current path
  const { user, isLoading: isAuthLoading } = useAuth(); // Removed logout from destructuring
  const {
    organizations,
    selectedOrganizationId,
    setSelectedOrganizationId,
    isLoadingOrgs,
    // isSuperAdminContext is used below, but we'll check user.roles directly in useEffect
  } = useOrganization();

  const [isReady, setIsReady] = useState(false); // State to track if auth/org checks are done

  // Parse orgId from URL, ensuring it's a number
  const currentOrgIdParam = params.orgId as string;
  const currentOrgId = parseInt(currentOrgIdParam, 10);

  useEffect(() => {
    // 1. Authentication Check
    if (!isAuthLoading && !user) {
      console.log("OrgLayout: Not authenticated, redirecting to login.");
      router.replace('/app/login');
      return; // Stop further processing if redirecting
    }

    // Determine SuperAdmin status *inside* the effect after checking user exists
    const isSuperAdmin = user?.roles?.includes('SuperAdmin') ?? false;

    // Wait until auth is done, and orgs are loaded *if* the user is a SuperAdmin
    if (isAuthLoading || (isSuperAdmin && isLoadingOrgs)) {
        console.log(`OrgLayout: Waiting for auth (${isAuthLoading}) / orgs (${isLoadingOrgs}) for user (isSuperAdmin: ${isSuperAdmin})...`);
        setIsReady(false);
        return;
    }

    // User is authenticated and orgs (if needed) are loaded
    console.log("OrgLayout: Auth/Orgs loaded. Proceeding with checks.");

    // 2. Authorization & Org Context Sync
    if (isNaN(currentOrgId)) {
        console.error("OrgLayout: Invalid orgId in URL (NaN). Redirecting...");
        // Decide on a fallback - maybe user's own org or login?
        if (user?.organizationId) {
            router.replace(`/app/org/${user.organizationId}/admin`); // Redirect to user's org
        } else {
            router.replace('/app/login'); // Fallback to login
        }
        return;
    }

    const userOrgId = user?.organizationId ? parseInt(user.organizationId, 10) : null;

    if (isSuperAdmin) { // Use the locally determined isSuperAdmin flag
        // SuperAdmin Logic
        console.log("OrgLayout: SuperAdmin detected.");
        const isValidOrg = organizations.some(org => org.id === currentOrgId);

        if (isValidOrg) {
            console.log(`OrgLayout: SuperAdmin - Valid org ${currentOrgId}. Syncing context.`);
            if (selectedOrganizationId !== currentOrgId) {
                setSelectedOrganizationId(currentOrgId);
            }
            setIsReady(true); // Ready to render content
        } else {
            console.log(`OrgLayout: SuperAdmin - Invalid org ${currentOrgId}. Redirecting...`);
            if (organizations.length > 0) {
                // Redirect to the first available org, preserving sub-path
                const firstOrgId = organizations[0].id;
                const newPath = pathname.replace(`/app/org/${currentOrgIdParam}`, `/app/org/${firstOrgId}`);
                router.replace(newPath);
            } else {
                // No organizations available for SuperAdmin? Show error or redirect?
                console.error("OrgLayout: SuperAdmin has no organizations available!");
                // Maybe redirect to a dedicated page or show an error message
                 setIsReady(true); // Allow rendering children which might show an error
            }
            // Don't set setIsReady(true) yet if redirecting
        }
    } else if (user) {
        // Non-SuperAdmin Logic (Admin, etc.)
        console.log("OrgLayout: Non-SuperAdmin detected.");
        if (userOrgId && currentOrgId === userOrgId) {
            console.log(`OrgLayout: Non-SuperAdmin - Org ${currentOrgId} matches user's org. Allowed.`);
            // Sync the context for non-SuperAdmins as well
            if (selectedOrganizationId !== currentOrgId) {
                 setSelectedOrganizationId(currentOrgId);
            }
            setIsReady(true); // Ready to render content
        } else {
            console.log(`OrgLayout: Non-SuperAdmin - Mismatched org ${currentOrgId} (user has ${userOrgId}). Redirecting...`);
            if (userOrgId) {
                 // Redirect to their correct org, preserving sub-path
                 const newPath = pathname.replace(`/app/org/${currentOrgIdParam}`, `/app/org/${userOrgId}`);
                 router.replace(newPath);
            } else {
                // User doesn't have an org ID? Error state.
                console.error("OrgLayout: Non-SuperAdmin user has no organizationId!");
                router.replace('/app/login'); // Fallback to login
            }
             // Don't set setIsReady(true) yet if redirecting
        }
    } else {
        // Should not happen if auth check passed, but as a safeguard
        console.log("OrgLayout: User object missing after auth check passed. Redirecting to login.");
        router.replace('/app/login');
    }

  }, [ // Dependencies might need adjustment - remove isSuperAdminContext, keep user
    user, isAuthLoading, isLoadingOrgs, organizations,
    currentOrgId, currentOrgIdParam, selectedOrganizationId,
    setSelectedOrganizationId, router, pathname // Removed isSuperAdminContext
  ]);

  // Removed handleOrgChange and handleLogout as they belong to the UI layout

  // Render loading state until checks are complete
  if (!isReady) {
    return (
      <div className="flex h-screen">
        <aside className="w-64 bg-gray-100 dark:bg-gray-800 p-4 border-r border-gray-200 dark:border-gray-700">
          <Skeleton className="h-8 w-3/4 mb-6" />
          <Skeleton className="h-6 w-full mb-4" />
          <Skeleton className="h-6 w-full mb-4" />
          <Skeleton className="h-6 w-full mb-4" />
          <Skeleton className="h-6 w-full mb-4" />
        </aside>
        <main className="flex-1 p-6">
          <Skeleton className="h-10 w-1/4 mb-4" />
          <Skeleton className="h-64 w-full" />
        </main>
      </div>
    );
  }

  // Render children directly once checks are complete and ready
  // The specific UI (sidebar, header) will be provided by nested layouts (e.g., AdminLayout)
  // Wrap children with SessionProvider
  return <>{children}</>;
}
