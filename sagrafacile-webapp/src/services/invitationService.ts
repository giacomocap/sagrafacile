import apiClient from './apiClient';
import { UserInvitationRequestDto, PendingInvitationDto, InvitationDetailsDto, AcceptInvitationDto } from '@/types';

export const invitationService = {
  // Send an invitation
  async inviteUser(invitation: UserInvitationRequestDto): Promise<any> {
    const response = await apiClient.post('/accounts/invite', invitation);
    return response.data;
  },

  // Get pending invitations for the current organization
  async getPendingInvitations(): Promise<PendingInvitationDto[]> {
    const response = await apiClient.get<PendingInvitationDto[]>('/accounts/pending-invitations');
    return response.data;
  },

  // Revoke a pending invitation
  async revokeInvitation(invitationId: string): Promise<void> {
    await apiClient.delete(`/accounts/invitations/${invitationId}`);
  },

  // Get invitation details (for the accept invitation page)
  async getInvitationDetails(token: string): Promise<InvitationDetailsDto> {
    const response = await apiClient.get<InvitationDetailsDto>(`/accounts/invitation-details?token=${token}`);
    return response.data;
  },

  // Accept an invitation
  async acceptInvitation(acceptData: AcceptInvitationDto): Promise<void> {
    await apiClient.post('/accounts/accept-invitation', acceptData);
  }
};
