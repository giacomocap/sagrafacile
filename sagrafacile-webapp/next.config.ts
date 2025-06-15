import type { NextConfig } from "next";

// This variable should be set in your environment (e.g., via docker-compose.yml for the frontend service).
// const appDomain = process.env.MY_DOMAIN ? process.env.MY_DOMAIN.trim() : undefined; // No longer needed for images config

// MY_DOMAIN related checks are no longer needed for images config
// if (!appDomain && process.env.NODE_ENV === 'production') {
//   console.warn("Warning: MY_DOMAIN environment variable is not set during the build. It is expected to be available at runtime for production image optimization to work correctly.");
// } else if (!appDomain) {
//   console.warn("Warning: MY_DOMAIN environment variable is not set. Remote image optimization for the primary domain might not work. This is expected if running locally without Docker and MY_DOMAIN.");
// } else {
//   console.log(appDomain)
// }

// Remote patterns configuration is no longer needed as we are not using Next.js Image optimization.
// type InferredRemotePattern = NonNullable<NonNullable<NextConfig['images']>['remotePatterns']>[0];
// const remotePatternsConfig: InferredRemotePattern[] = [];
// if (appDomain) {
//   remotePatternsConfig.push({
//     protocol: 'https', 
//     hostname: appDomain as string, 
//     pathname: '/media/**', 
//   });
// }
// remotePatternsConfig.push({
//   protocol: 'http',
//   hostname: 'api', 
//   port: '8080', 
//   pathname: '/media/**', 
// });
// const localBackendPatterns = [
//   { hostname: '192.168.1.38', port: '7075' },
//   { hostname: '192.168.1.237', port: '7075' },
//   { hostname: '192.168.1.24', port: '7075' },
// ];
// localBackendPatterns.forEach(pattern => {
//   remotePatternsConfig.push({
//     protocol: 'http',
//     hostname: pattern.hostname,
//     port: pattern.port,
//     pathname: '/media/**',
//   });
//   remotePatternsConfig.push({
//     protocol: 'https',
//     hostname: pattern.hostname,
//     port: pattern.port,
//     pathname: '/media/**',
//   });
// });

const nextConfig: NextConfig = {
  allowedDevOrigins: ["http://192.168.1.219:3000", "https://192.168.1.219:3000", "https://192.168.1.38", "192.168.1.24"],
  // images: { // Configuration for Next.js Image Optimization removed
  //   remotePatterns: remotePatternsConfig,
  //   dangerouslyAllowSVG: true, // This was part of images config, can be removed if not needed elsewhere
  //   contentDispositionType: 'attachment', // This was part of images config
  // },
};

export default nextConfig;
