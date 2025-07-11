import { OrganizationDto } from '@/types';
import apiClient from './apiClient';

export const getOrganizationById = async (orgId: string): Promise<OrganizationDto> => {
  const response = await apiClient.get<OrganizationDto>(`/organizations/${orgId}`);
  return response.data;
};

interface OrganizationProvisionRequest {
  organizationName: string;
}

const provisionOrganization = async (
  data: OrganizationProvisionRequest
): Promise<OrganizationDto> => {
  const response = await apiClient.post<OrganizationDto>('/organizations/provision', data);
  return response.data;
};

const organizationService = {
  getOrganizationById,
  provisionOrganization,
};

export default organizationService;
