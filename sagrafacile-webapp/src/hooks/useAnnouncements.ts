import { useCallback, useRef } from 'react';
import { toast } from 'sonner';

const DEFAULT_NOTIFICATION_SOUND_URL = '/sounds/pickup-chime.mp3'; // Default sound

interface UseAnnouncementsProps {
  soundUrl?: string;
  speechLang?: string;
  speechRate?: number;
  speechPitch?: number;
}

export default function useAnnouncements(props?: UseAnnouncementsProps) {
  const {
    soundUrl = DEFAULT_NOTIFICATION_SOUND_URL,
    speechLang = 'it-IT',
    speechRate = 0.9,
    speechPitch = 1.0,
  } = props || {};

  const audioRef = useRef<HTMLAudioElement | null>(null);
  const isAudioContextUnlocked = useRef(false);

  // Function to unlock the audio context, must be called after a user interaction
  const unlockAudio = useCallback(() => {
    if (isAudioContextUnlocked.current) return;
    
    if (!audioRef.current) {
        audioRef.current = new Audio(soundUrl);
    }
    
    // Play a tiny silent sound to unlock the audio context
    audioRef.current.muted = true;
    const promise = audioRef.current.play();

    if (promise !== undefined) {
        promise.then(() => {
            // Autoplay started!
            audioRef.current?.pause();
            audioRef.current!.currentTime = 0;
            audioRef.current!.muted = false;
            isAudioContextUnlocked.current = true;
            console.log("Audio context unlocked successfully.");
        }).catch(error => {
            console.error("Audio context unlock failed:", error);
            // We can't do much here, but subsequent plays might still fail
        });
    }
  }, [soundUrl]);

  const playNotificationSound = useCallback(() => {
    if (!isAudioContextUnlocked.current) {
        console.warn("Audio context not unlocked. Sound may not play.");
        // Don't toast here, as it would be annoying on every call. 
        // The UI should handle prompting the user to unlock.
        return;
    }
    if (!audioRef.current) {
      audioRef.current = new Audio(soundUrl);
    }
    audioRef.current.play().catch(err => {
      console.error("Error playing notification sound:", err);
      // Avoid toasting if it's the common NotAllowedError, as the unlock should handle it.
      if ((err as DOMException).name !== 'NotAllowedError') {
        toast.info("Impossibile riprodurre il suono. Controlla le autorizzazioni del browser.");
      }
    });
  }, [soundUrl]);

  const speakAnnouncement = useCallback((orderId: string, customerName?: string | null, orderIdLength: number = 4) => {
    if ('speechSynthesis' in window) {
      const orderDisplayId = orderId.substring(orderId.length - Math.min(orderIdLength, orderId.length)).toUpperCase();
      const announcementText = customerName
        ? `Ordine ${orderDisplayId} per ${customerName} è pronto!`
        : `Ordine ${orderDisplayId} è pronto!`;

      const utterance = new SpeechSynthesisUtterance(announcementText);
      utterance.lang = speechLang;
      utterance.rate = speechRate;
      utterance.pitch = speechPitch;
      window.speechSynthesis.speak(utterance);
    } else {
      console.warn("Browser does not support Speech Synthesis.");
      toast.warning("Il browser non supporta gli annunci vocali.");
    }
  }, [speechLang, speechRate, speechPitch]);

  const speakRawText = useCallback((text: string) => {
    if ('speechSynthesis' in window) {
      const utterance = new SpeechSynthesisUtterance(text);
      utterance.lang = speechLang;
      utterance.rate = speechRate;
      utterance.pitch = speechPitch;
      window.speechSynthesis.speak(utterance);
    } else {
      console.warn("Browser does not support Speech Synthesis.");
      toast.warning("Il browser non supporta gli annunci vocali.");
    }
  }, [speechLang, speechRate, speechPitch]);

  return { playNotificationSound, speakAnnouncement, speakRawText, unlockAudio };
}
