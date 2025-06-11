'use client';

import React, { useState, useEffect, useRef, useCallback } from 'react';
import Image from 'next/image';

// Define the structure for a media item in the carousel
export interface AdMedia {
  type: 'image' | 'video';
  src: string;
  durationSeconds?: number; // Duration for images
}

interface AdCarouselProps {
  mediaItems: AdMedia[];
}

const AdCarousel: React.FC<AdCarouselProps> = ({ mediaItems }) => {
  const [currentIndex, setCurrentIndex] = useState(0);
  const videoRefs = useRef<(HTMLVideoElement | null)[]>([]);

  const goToNextItem = useCallback(() => {
    if (mediaItems.length > 0) {
      setCurrentIndex((prevIndex) => (prevIndex + 1) % mediaItems.length);
    }
  }, [mediaItems.length]);

  // Effect to handle the rotation logic and video playback
  useEffect(() => {
    if (mediaItems.length === 0) return;

    // Pause all non-current videos to prevent background playback
    videoRefs.current.forEach((video, index) => {
      if (video && index !== currentIndex) {
        video.pause();
      }
    });

    const currentItem = mediaItems[currentIndex];
    const videoElement = videoRefs.current[currentIndex];

    if (currentItem.type === 'image') {
      const timer = setTimeout(goToNextItem, (currentItem.durationSeconds || 15) * 1000);
      return () => clearTimeout(timer);
    } else if (currentItem.type === 'video' && videoElement) {
      // When a video becomes the active item, we ensure it plays from the start.
      // The `autoPlay` attribute is not always reliable for subsequent plays.
      videoElement.currentTime = 0;
      videoElement.play().catch((error) => {
        console.error(`Error attempting to play video: ${currentItem.src}`, error);
        // Skip to the next item if autoplay fails for any reason
        goToNextItem();
      });
    }
  }, [currentIndex, mediaItems, goToNextItem]);

  if (mediaItems.length === 0) {
    return null; // Don't render anything if there are no ads
  }

  return (
    <div className="relative w-full h-full bg-transparent overflow-hidden">
      {mediaItems.map((item, index) => (
        <div
          key={index}
          className={`absolute inset-0 transition-opacity duration-1000 ${
            index === currentIndex ? 'opacity-100' : 'opacity-0'
          }`}
        >
          {item.type === 'image' && (
            <Image
              src={item.src}
              alt={`Ad image ${index + 1}`}
              fill
              style={{ objectFit: 'contain' }}
              priority={index === 0} // Prioritize loading the first image
            />
          )}
          {item.type === 'video' && (
            <video
              ref={(el) => {
                videoRefs.current[index] = el;
              }}
              src={item.src}
              className="w-full h-full object-contain"
              autoPlay
              muted
              playsInline
              onEnded={goToNextItem}
              onError={() => {
                console.error(`Failed to load video: ${item.src}`);
                goToNextItem(); // Skip to next item on error
              }}
            />
          )}
        </div>
      ))}
    </div>
  );
};

export default AdCarousel;
