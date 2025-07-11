import apiClient from './apiClient';

export interface InstanceInfo {
  mode: 'saas' | 'opensource';
}

export const getInstanceInfo = async (): Promise<InstanceInfo> => {
  const response = await apiClient.get<InstanceInfo>('/instance/info');
  return response.data;
};
