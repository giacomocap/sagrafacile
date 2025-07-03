import { OrganizationDto } from '@/types';
import apiClient from './apiClient';

export const getOrganizationById = async (orgId: string): Promise<OrganizationDto> => {
  const response = await apiClient.get<OrganizationDto>(`/organizations/${orgId}`);
  return response.data;
};
