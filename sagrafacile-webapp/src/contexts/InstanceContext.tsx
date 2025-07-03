'use client';

import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { getInstanceInfo, InstanceInfo } from '@/services/instanceService';

interface InstanceContextType {
  instanceInfo: InstanceInfo | null;
  loading: boolean;
}

const InstanceContext = createContext<InstanceContextType | undefined>(undefined);

export const InstanceProvider = ({ children }: { children: ReactNode }) => {
  const [instanceInfo, setInstanceInfo] = useState<InstanceInfo | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchInstanceInfo = async () => {
      try {
        const info = await getInstanceInfo();
        setInstanceInfo(info);
      } catch (error) {
        console.error("Failed to fetch instance info:", error);
        // Fallback to opensource mode on error
        setInstanceInfo({ mode: 'opensource' });
      } finally {
        setLoading(false);
      }
    };

    fetchInstanceInfo();
  }, []);

  return (
    <InstanceContext.Provider value={{ instanceInfo, loading }}>
      {children}
    </InstanceContext.Provider>
  );
};

export const useInstance = (): InstanceContextType => {
  const context = useContext(InstanceContext);
  if (context === undefined) {
    throw new Error('useInstance must be used within an InstanceProvider');
  }
  return context;
};
