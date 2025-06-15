// Custom image loader for Next.js to handle internal API routing
// This loader transforms public domain URLs to internal API service URLs
// when running inside the Docker container environment

interface ImageLoaderProps {
  src: string;
  width: number;
  quality?: number;
}

export default function imageLoader({ src, width, quality }: ImageLoaderProps): string {
  // Get the domain from environment variable
  const appDomain = process.env.MY_DOMAIN?.trim();
  
  // If the source URL is from our public domain, transform it to use the internal API service
  if (appDomain && src.startsWith(`https://${appDomain}/media/`)) {
    // Transform: https://app.sagrafacile.it/media/... -> http://api:8080/media/...
    const internalUrl = src.replace(`https://${appDomain}`, 'http://api:8080');
    
    // For debugging - log the transformation
    console.log(`[ImageLoader] Transforming URL: ${src} -> ${internalUrl} (width: ${width}, quality: ${quality || 75})`);
    
    return internalUrl;
  }
  
  // For local development IPs or other domains, return as-is
  console.log(`[ImageLoader] Using original URL: ${src} (width: ${width}, quality: ${quality || 75})`);
  return src;
}
