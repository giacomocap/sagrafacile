import apiClient from './apiClient';
import { SyncConfigurationDto, SyncConfigurationUpsertDto, MenuSyncResult } from '@/types';
import { AxiosResponse } from 'axios';

/**
 * Fetches the sync configuration for an organization.
 * GET /api/sync/organizations/{organizationId}/config
 * @param organizationId The ID of the organization
 * @returns The sync configuration or null if not found
 */
export const getSyncConfiguration = async (organizationId: number): Promise<SyncConfigurationDto | null> => {
  try {
    const response: AxiosResponse<SyncConfigurationDto> = await apiClient.get(
      `/sync/organizations/${organizationId}/config`
    );
    return response.data;
  } catch (error: any) {
    if (error.response && error.response.status === 404) {
      return null; // Return null if no configuration exists
    }
    console.error("Error fetching sync configuration:", error);
    throw error;
  }
};

/**
 * Creates or updates the sync configuration for an organization.
 * PUT /api/sync/organizations/{organizationId}/config
 * @param organizationId The ID of the organization
 * @param config The sync configuration to create or update
 * @returns The saved sync configuration
 */
export const upsertSyncConfiguration = async (
  organizationId: number,
  config: SyncConfigurationUpsertDto
): Promise<SyncConfigurationDto> => {
  const response: AxiosResponse<SyncConfigurationDto> = await apiClient.put(
    `/sync/organizations/${organizationId}/config`,
    config
  );
  return response.data;
};

/**
 * Deletes the sync configuration for an organization.
 * DELETE /api/sync/organizations/{organizationId}/config
 * @param organizationId The ID of the organization
 */
export const deleteSyncConfiguration = async (organizationId: number): Promise<void> => {
  await apiClient.delete(`/sync/organizations/${organizationId}/config`);
};

/**
 * Triggers menu synchronization for an organization.
 * POST /api/sync/organizations/{organizationId}/sync/menu
 * @param organizationId The ID of the organization
 * @returns The result of the synchronization
 */
export const syncMenu = async (organizationId: number): Promise<MenuSyncResult> => {
  const response: AxiosResponse<MenuSyncResult> = await apiClient.post(
    `/sync/organizations/${organizationId}/sync/menu`
  );
  return response.data;
};
