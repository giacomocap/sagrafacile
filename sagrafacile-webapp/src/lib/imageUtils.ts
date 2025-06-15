// Utility functions for handling image URLs in Docker environment
// This ensures Next.js Image optimization works correctly with internal API routing

/**
 * Transforms a public media URL to an internal API URL for Next.js Image optimization
 * This allows Next.js to fetch images directly from the API service within Docker network
 */
export function getOptimizedImageUrl(publicUrl: string): string {
  // Get the domain from environment variable
  const appDomain = process.env.MY_DOMAIN?.trim();
  
  // If the URL is from our public domain, transform it to use the internal API service
  if (appDomain && publicUrl.startsWith(`https://${appDomain}/media/`)) {
    // Transform: https://app.sagrafacile.it/media/... -> http://api:8080/media/...
    const internalUrl = publicUrl.replace(`https://${appDomain}`, 'http://api:8080');
    
    console.log(`[ImageUtils] Transforming for optimization: ${publicUrl} -> ${internalUrl}`);
    return internalUrl;
  }
  
  // For other URLs (local development, external images), return as-is
  return publicUrl;
}

/**
 * Gets the media URL for a given file path
 * Returns the appropriate URL based on environment (public domain or internal API)
 */
export function getMediaUrl(filePath: string): string {
  const appDomain = process.env.MY_DOMAIN?.trim();
  
  // Handle file paths that already include /media/ prefix
  const cleanPath = filePath.startsWith('/media/') ? filePath : `/media/${filePath}`;
  
  if (appDomain) {
    // In production with domain, use public HTTPS URL
    return `https://${appDomain}${cleanPath}`;
  }
  
  // Fallback for local development
  return cleanPath;
}

/**
 * Gets the internal API URL for media files (for Next.js Image optimization)
 * In Docker environment, uses internal API service URL
 * In local development, uses backend API URL
 */
export function getInternalMediaUrl(filePath: string): string {
  const appDomain = process.env.MY_DOMAIN?.trim();
  
  // Handle file paths that already include /media/ prefix
  const cleanPath = filePath.startsWith('/media/') ? filePath : `/media/${filePath}`;
  
  // In Docker production environment with MY_DOMAIN set
  if (appDomain && process.env.NODE_ENV === 'production') {
    // Use internal API URL for Next.js Image optimization
    return `http://api:8080${cleanPath}`;
  }
  
  // In local development, use the backend API URL
  const apiBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL;
  if (apiBaseUrl) {
    // Remove /api suffix if present and add the media path
    const baseUrl = apiBaseUrl.replace(/\/api$/, '');
    return `${baseUrl}${cleanPath}`;
  }
  
  // Final fallback
  return cleanPath;
}
