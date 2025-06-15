// Utility functions for handling image URLs

/**
 * Gets the media URL for a given file path.
 * Returns the appropriate URL based on environment configuration.
 */
export function getMediaUrl(filePath: string): string {
  const appDomain = process.env.MY_DOMAIN?.trim();
  
  // Ensure filePath starts with /media/ and handles potential double slashes if filePath already includes it.
  let cleanPath = filePath;
  if (filePath.startsWith('/media/')) {
    // Already good
  } else if (filePath.startsWith('media/')) {
    cleanPath = `/${filePath}`;
  } else {
    cleanPath = `/media/${filePath}`;
  }

  if (appDomain) {
    // In production with domain, use public HTTPS URL
    return `https://${appDomain}${cleanPath}`;
  }
  
  // Fallback to NEXT_PUBLIC_API_BASE_URL (removing /api suffix) if MY_DOMAIN is not set
  const apiBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL;
  if (apiBaseUrl) {
    // Remove /api suffix if present and add the media path
    const baseUrl = apiBaseUrl.replace(/\/api$/, '');
    return `${baseUrl}${cleanPath}`;
  }
  
  // Final fallback (e.g., for local development if no specific base URL is configured for assets)
  // This will result in a relative path like /media/image.png
  return cleanPath;
}