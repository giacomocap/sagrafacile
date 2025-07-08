'use client';

import React, { createContext, useState, useContext, ReactNode, useEffect, useCallback } from 'react';
import apiClient from '@/services/apiClient';
import { useAuth } from './AuthContext'; // To check if user is SuperAdmin
import { DayDto } from '@/types'; // Import DayDto
import { getCurrentOpenDay } from '@/services/dayService'; // Import API function

interface Organization {
    id: string;
    name: string;
}

interface OrganizationContextType {
    organizations: Organization[];
    selectedOrganizationId: string | null;
    setSelectedOrganizationId: (id: string | null) => void;
    currentOrganization: Organization | null;
    isLoadingOrgs: boolean;
    orgError: string | null;
    isSuperAdminContext: boolean; // Expose if the context is active for a SuperAdmin
    refreshOrganizations: () => Promise<void>; // Function to manually refresh organizations
    // Day state
    currentDay: DayDto | null;
    isLoadingDay: boolean;
    dayError: string | null;
    refreshCurrentDay: () => Promise<void>; // Function to manually refresh day status
}

const OrganizationContext = createContext<OrganizationContextType | undefined>(undefined);

export const OrganizationProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
    const { user } = useAuth();
    const [organizations, setOrganizations] = useState<Organization[]>([]);
    const [selectedOrganizationId, setSelectedOrganizationId] = useState<string | null>(null);
    const [currentOrganization, setCurrentOrganization] = useState<Organization | null>(null);
    const [isLoadingOrgs, setIsLoadingOrgs] = useState<boolean>(true); // Start true for initial load
    const [orgError, setOrgError] = useState<string | null>(null);
    // Day state
    const [currentDay, setCurrentDay] = useState<DayDto | null>(null);
    const [isLoadingDay, setIsLoadingDay] = useState<boolean>(true); // Start true for initial load
    const [dayError, setDayError] = useState<string | null>(null);

    const isSuperAdminContext = user?.roles?.includes('SuperAdmin') ?? false

    const fetchOrganizations = useCallback(async () => {
        if (!user) {
            setOrganizations([]);
            setSelectedOrganizationId(null);
            setIsLoadingOrgs(false);
            return;
        }

        setIsLoadingOrgs(true);
        setOrgError(null);
        try {
            const response = await apiClient.get<Organization[]>('/Organizations');
            setOrganizations(response.data);
            // Optionally set a default selection or leave null
            if (response.data.length > 0) {
                setSelectedOrganizationId(response.data[0].id);
            } else {
                setSelectedOrganizationId(null);
            }
        } catch (err: any) {
            console.error("Failed to fetch organizations:", err);
            setOrgError(err.response?.data?.message || err.message || 'Failed to fetch organizations.');
            setOrganizations([]); // Clear orgs on error
            setSelectedOrganizationId(null); // Clear selection on error
        } finally {
            setIsLoadingOrgs(false);
        }
    }, [user]);

    // Fetch current operational day
    const fetchCurrentDay = useCallback(async () => {
        // *** ADDED: Explicitly prevent fetching day for SuperAdmin context ***
        if (isSuperAdminContext) {
            setCurrentDay(null);
            setIsLoadingDay(false);
            setDayError(null);
            return;
        }
        // Don't fetch if no user (already handled by SuperAdmin check above for that case)
        // or if SuperAdmin hasn't selected an org yet (this condition is now redundant due to the check above)
        if (!user /* || (isSuperAdminContext && !selectedOrganizationId) */) {
            setCurrentDay(null);
            setIsLoadingDay(false);
            setDayError(null);
            return;
        }

        setIsLoadingDay(true);
        setDayError(null);
        try {
            // getCurrentOpenDay uses the apiClient which includes the token
            // The backend determines the correct organization context based on the token/role
            const dayData = await getCurrentOpenDay();
            setCurrentDay(dayData); // Will be null if no day is open or on 404
        } catch (err: any) {
            console.error("Failed to fetch current day:", err);
            // Don't set a blocking error unless it's unexpected
            if (err.response?.status !== 404) {
                setDayError(err.message || 'Failed to fetch current day status.');
            }
            setCurrentDay(null);
        } finally {
            setIsLoadingDay(false);
        }
    }, [user, isSuperAdminContext]); // Dependencies for the fetch logic

    // Effect to fetch organizations when user changes
    useEffect(() => {
        fetchOrganizations();
    }, [fetchOrganizations]);

    // Effect to fetch the current day when user or selected org changes
    useEffect(() => {
        fetchCurrentDay();
    }, [fetchCurrentDay]); // Run whenever the fetch function identity changes (due to its dependencies)

    // Clear selection when user logs out or changes
    useEffect(() => {
        if (!user) {
            setSelectedOrganizationId(null);
            setOrganizations([]);
            setCurrentDay(null); // Clear day state on logout
            setDayError(null);
        } else {
            // If user changes but is not SuperAdmin, reset selectedOrgId (it's implicit)
            // This might trigger the day fetch effect correctly
            if (!isSuperAdminContext) {
                setSelectedOrganizationId(null);
            }
        }
        // We fetch day state in a separate effect based on user and selectedOrgId
    }, [user, isSuperAdminContext]);

    useEffect(() => {
        if (selectedOrganizationId) {
            const org = organizations.find(o => o.id === selectedOrganizationId);
            setCurrentOrganization(org || null);
        } else if (user && !isSuperAdminContext && organizations.length > 0) {
            // For non-superadmin, if no org is selected, default to the user's org
            const userOrg = organizations.find(o => o.id === user.organizationId);
            setCurrentOrganization(userOrg || null);
        }
        else {
            setCurrentOrganization(null);
        }
    }, [selectedOrganizationId, organizations, user, isSuperAdminContext]);

    const handleSetSelectedOrganizationId = (id: string | null) => {
        setSelectedOrganizationId(id);
        // Potentially save to localStorage? Or rely on context state.
    };

    return (
        <OrganizationContext.Provider value={{
            organizations,
            selectedOrganizationId,
            setSelectedOrganizationId: handleSetSelectedOrganizationId,
            currentOrganization,
            isLoadingOrgs,
            orgError,
            isSuperAdminContext,
            refreshOrganizations: fetchOrganizations, // Expose refresh function
            // Day state
            currentDay,
            isLoadingDay,
            dayError,
            refreshCurrentDay: fetchCurrentDay // Expose day refresh function
        }}>
            {children}
        </OrganizationContext.Provider>
    );
};

export const useOrganization = (): OrganizationContextType => {
    const context = useContext(OrganizationContext);
    if (context === undefined) {
        throw new Error('useOrganization must be used within an OrganizationProvider');
    }
    return context;
};
