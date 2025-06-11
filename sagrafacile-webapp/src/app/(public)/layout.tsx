import React from 'react';
import '../globals.css'; // Assuming global styles are here

export default function PublicLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  // This layout wraps public-facing pages that don't require authentication.
  // It should NOT include <html> or <body> tags, as those are handled by the root layout.
  // The main container styling (like padding) is removed to allow child pages
  // like the QueueDisplayPage to control their own full-screen layouts.
  return <main>{children}</main>;
}
