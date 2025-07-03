import type { Metadata } from "next";
import { GeistSans } from "geist/font/sans";
import { GeistMono } from "geist/font/mono";
import "./globals.css";
import { cn } from "../lib/utils"; // Import cn utility
import { AuthProvider } from "../contexts/AuthContext";
import { OrganizationProvider } from "../contexts/OrganizationContext";
import { InstanceProvider } from "../contexts/InstanceContext";
import { Toaster } from "@/components/ui/sonner";

// Define font variables using the recommended geist/font/* imports
const geistSans = GeistSans;
const geistMono = GeistMono;

export const metadata: Metadata = {
  title: "SagraFacile",
  description: "Gestionale per Sagre e Eventi Gastronomici gratuito",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    // Added suppressHydrationWarning for potential theme extensions later
    <html lang="en" suppressHydrationWarning>
      <body
        className={cn(
          "min-h-screen bg-background font-sans antialiased",
          geistSans.variable, // Use the variable property from the font object
          geistMono.variable  // Use the variable property from the font object
        )}
      >
        <InstanceProvider>
          <AuthProvider>
            <OrganizationProvider>{children}</OrganizationProvider>
          </AuthProvider>
        </InstanceProvider>
        {/* Move toast position to avoid overlap */}
        <Toaster richColors position="top-center" />
      </body>
    </html>
  );
}
