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

// Add internal API service pattern for direct access from frontend container
// This allows Next.js to fetch images directly from the API service without going through Caddy
remotePatternsConfig.push({
  protocol: 'http', // Internal Docker network uses HTTP
  hostname: 'api', // Internal service name
  port: '8080', // Internal API port
  pathname: '/media/**', // Allow all images served under /media/
});

// Add local development backend API patterns
// Common local development URLs for the backend API
const localBackendPatterns = [
  { hostname: '192.168.1.38', port: '7075' },
  { hostname: '192.168.1.237', port: '7075' },
  { hostname: '192.168.1.24', port: '7075' },
];

localBackendPatterns.forEach(pattern => {
  remotePatternsConfig.push({
    protocol: 'http',
    hostname: pattern.hostname,
    port: pattern.port,
    pathname: '/media/**',
  });
  remotePatternsConfig.push({
    protocol: 'https',
    hostname: pattern.hostname,
    port: pattern.port,
    pathname: '/media/**',
  });
});

const nextConfig: NextConfig = {
  allowedDevOrigins: ["http://192.168.1.219:3000", "https://192.168.1.219:3000", "https://192.168.1.38"],
  images: {
    remotePatterns: remotePatternsConfig,
    // Configure Next.js to use internal API for image fetching during optimization
    dangerouslyAllowSVG: true,
    contentDispositionType: 'attachment',
  },
};

export default nextConfig;
