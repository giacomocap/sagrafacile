import type { NextConfig } from "next";

// This variable should be set in your environment (e.g., via docker-compose.yml for the frontend service).
const appDomain = process.env.MY_DOMAIN;

if (!appDomain && process.env.NODE_ENV === 'production') {
  // In production, MY_DOMAIN should always be set.
  throw new Error("MY_DOMAIN environment variable is not set. This is required for Next.js image optimization in production.");
} else if (!appDomain) {
  console.warn("Warning: MY_DOMAIN environment variable is not set. Remote image optimization for the primary domain might not work. This is expected if running locally without Docker and MY_DOMAIN.");
}

// Infer the type from NextConfig['images']['remotePatterns']
// The 'NonNullable' and array indexing '[0]' are used to get the type of a single pattern object.
type InferredRemotePattern = NonNullable<NonNullable<NextConfig['images']>['remotePatterns']>[0];

const remotePatternsConfig: InferredRemotePattern[] = [];

if (appDomain) {
  remotePatternsConfig.push({
    protocol: 'https', // Your domain uses HTTPS via Caddy
    hostname: appDomain as string, // The actual domain name, e.g., app.sagrafacile.it
    // port: '', // Not needed for standard HTTPS port 443. If your domain uses a non-standard port for HTTPS, specify it here as a string.
    pathname: '/media/**', // Allow all images served under /media/
  });
}

// Existing patterns below are likely for local development or specific IP access.
// Add them to the remotePatternsConfig array
remotePatternsConfig.push(
  {
    protocol: 'https',
    hostname: '192.168.1.38', // Example local IP
    port: '7075',
    pathname: '/media/**',
  },
  {
    protocol: 'https',
    hostname: '192.168.1.237', // Example local IP
    port: '7075',
    pathname: '/media/**',
  }
);

const nextConfig: NextConfig = {
  allowedDevOrigins: ["http://192.168.1.219:3000", "https://192.168.1.219:3000","https://192.168.1.38"],
  images: {
    remotePatterns: remotePatternsConfig,
  },
};

export default nextConfig;
