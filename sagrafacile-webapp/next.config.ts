import type { NextConfig } from "next";

// This variable should be set in your environment (e.g., via docker-compose.yml for the frontend service).
const appDomain = process.env.MY_DOMAIN ? process.env.MY_DOMAIN.trim() : undefined;

// MY_DOMAIN is expected to be set at runtime for production.
// During the build phase (when next build runs), MY_DOMAIN might not be available,
// and that's acceptable as the Next.js server will pick it up at runtime.
// We'll keep a warning if it's not set, but not throw an error during build.
if (!appDomain && process.env.NODE_ENV === 'production') {
  console.warn("Warning: MY_DOMAIN environment variable is not set during the build. It is expected to be available at runtime for production image optimization to work correctly.");
} else if (!appDomain) {
  console.warn("Warning: MY_DOMAIN environment variable is not set. Remote image optimization for the primary domain might not work. This is expected if running locally without Docker and MY_DOMAIN.");
} else {
  console.log(appDomain)
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
  allowedDevOrigins: ["http://192.168.1.219:3000", "https://192.168.1.219:3000", "https://192.168.1.38"],
  images: {
    remotePatterns: remotePatternsConfig,
  },
};

export default nextConfig;
