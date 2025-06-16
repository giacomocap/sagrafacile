import { useState, useEffect, useCallback } from 'react';
import apiClient from '@/services/apiClient';
import { AdAreaAssignmentDto } from '@/types';
import { AdMedia } from '@/components/public/AdCarousel';
import { getMediaUrl } from '@/lib/imageUtils';

interface UseAdsReturn {
  adMediaItems: AdMedia[];
  isLoading: boolean;
  error: string | null;
  refetch: () => void;
}

/**
 * Custom hook for fetching and managing ads for a specific area
 * @param areaId - The area ID to fetch ads for
 * @returns Object containing ad media items, loading state, error state, and refetch function
 */
export function useAds(areaId: string | null): UseAdsReturn {
  const [adMediaItems, setAdMediaItems] = useState<AdMedia[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchAds = useCallback(async () => {
    if (!areaId) {
      setAdMediaItems([]);
      setError(null);
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const response = await apiClient.get<AdAreaAssignmentDto[]>(`/public/areas/${areaId}/ads`);
      const transformedAds: AdMedia[] = response.data.map(ad => {
        const mediaUrl = getMediaUrl(ad.adMediaItem.filePath);
        return {
          type: ad.adMediaItem.mediaType.toLowerCase() as 'image' | 'video',
          src: mediaUrl,
          durationSeconds: ad.durationSeconds ?? undefined, // Coalesce null to undefined
        };
      });
      setAdMediaItems(transformedAds);
    } catch (err) {
      console.error("Failed to fetch ads:", err);
      // Don't show error to public users, just log it
      setError("Failed to load advertisements");
      setAdMediaItems([]);
    } finally {
      setIsLoading(false);
    }
  }, [areaId]);

  useEffect(() => {
    fetchAds();
  }, [fetchAds]);

  return {
    adMediaItems,
    isLoading,
    error,
    refetch: fetchAds,
  };
}
