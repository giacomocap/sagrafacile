'use client'; // Context needs to be client-side

import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { jwtDecode, JwtPayload } from 'jwt-decode'; // We'll need this to decode the token
import { TokenResponseDto } from '@/types'; // Import the DTO

// Define the shape of the JWT payload more specifically
interface CustomJwtPayload extends JwtPayload {
  nameid?: string; // ASP.NET Core Identity default for User ID
  // sub is already in JwtPayload
  email?: string; // Standard claim
  given_name?: string; // Standard claim for first name
  family_name?: string; // Standard claim for last name
  organizationId?: string; // Custom claim
  ['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']?: string | string[]; // ASP.NET Core Identity roles
}


// Define the shape of the user object derived from the JWT payload
// Adjust based on the actual claims in your JWT from the .NET backend
interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  organizationId: string;
  roles: string[]; // Assuming roles are stored as an array of strings in the token
  // Add other relevant claims like 'exp' if needed for client-side checks
}

// Define the shape of the context value
interface AuthContextType {
  user: User | null;
  accessToken: string | null;
  refreshToken: string | null;
  isLoading: boolean; // To handle initial token loading/validation
  login: (tokenResponse: TokenResponseDto) => void;
  logout: () => void;
  setTokens: (accessToken: string, refreshToken: string | null) => void; // For token refresh
}

// Create the context with a default value
const AuthContext = createContext<AuthContextType | undefined>(undefined);

// Define the props for the provider component
interface AuthProviderProps {
  children: ReactNode;
}

// Create the provider component
export const AuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true); // Start loading initially

  const processAndSetToken = (tokenToProcess: string, newRefreshToken?: string | null) => {
    try {
      const decoded = jwtDecode<CustomJwtPayload>(tokenToProcess);
      const isExpired = decoded.exp && decoded.exp * 1000 < Date.now();

      if (!isExpired) {
        const currentUser: User = {
          id: decoded.nameid || decoded.sub || '',
          email: decoded.email || '',
          firstName: decoded.given_name || '',
          lastName: decoded.family_name || '',
          organizationId: decoded.organizationId || '',
          roles: Array.isArray(decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'])
                 ? decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
                 : (decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ? [decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']] : []),
        };
        setAccessToken(tokenToProcess);
        setUser(currentUser);
        localStorage.setItem('authToken', tokenToProcess);

        if (newRefreshToken !== undefined) { // Only update refresh token if explicitly provided
          if (newRefreshToken) {
            setRefreshToken(newRefreshToken);
            localStorage.setItem('refreshToken', newRefreshToken);
          } else { // If newRefreshToken is null, clear it
            setRefreshToken(null);
            localStorage.removeItem('refreshToken');
          }
        }
        return true; // Token processed successfully
      } else {
        console.log("Token is expired.");
        return false; // Token was expired
      }
    } catch (error) {
      console.error("Failed to decode token or token is invalid:", error);
      return false; // Error during processing
    }
  };

  // Effect to load token and user from localStorage on initial mount
  useEffect(() => {
    setIsLoading(true);
    const storedAccessToken = localStorage.getItem('authToken');
    const storedRefreshToken = localStorage.getItem('refreshToken');

    if (storedAccessToken) {
      if (!processAndSetToken(storedAccessToken)) {
        // Access token was expired or invalid, clear it
        localStorage.removeItem('authToken');
        setAccessToken(null);
        setUser(null);
        // Also clear refresh token if access token fails, as we'll rely on refresh flow
        localStorage.removeItem('refreshToken');
        setRefreshToken(null);
      } else {
        // Access token is valid, also load refresh token if it exists
        if (storedRefreshToken) {
          setRefreshToken(storedRefreshToken);
        }
      }
    }
    setIsLoading(false);
  }, []);

  // Login function: stores tokens and decodes user info
  const login = (tokenResponse: TokenResponseDto) => {
    processAndSetToken(tokenResponse.accessToken, tokenResponse.refreshToken);
  };

  // Logout function: clears tokens and user info
  const logout = () => {
    localStorage.removeItem('authToken');
    localStorage.removeItem('refreshToken');
    setAccessToken(null);
    setRefreshToken(null);
    setUser(null);
    // Optionally redirect to login page or home page
  };

  // Function to update tokens, typically after a refresh
  const setTokens = (newAccessToken: string, newRefreshToken: string | null) => {
    processAndSetToken(newAccessToken, newRefreshToken);
  };

  // Provide the context value to children
  const value = { user, accessToken, refreshToken, isLoading, login, logout, setTokens };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

// Custom hook to use the AuthContext
export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
