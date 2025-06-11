'use client';

import React from 'react';
import NoDayOpenOverlay from '@/components/NoDayOpenOverlay'; // Import the overlay component

// This layout assumes the parent OrganizationLayout has already handled
// auth checks, org validation, and context setup.
// It provides the minimal shell for the Cashier interface.

export default function CashierLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  // Currently, no specific header or sidebar for the cashier view itself.
  // The CashierPage component uses flex layout to fill the space provided by OrganizationLayout.
  // We might add a simple header here later if needed (e.g., showing logged-in user).
  // Wrap the children with the overlay to apply blur/warning when no day is open.
  return (
    <NoDayOpenOverlay>
      {/* Render the CashierPage component (or area selection page) */}
      {children}
    </NoDayOpenOverlay>
  );
}
