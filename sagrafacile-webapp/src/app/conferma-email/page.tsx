'use client';

import React, { useEffect, useState, Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import apiClient from '@/services/apiClient';
import Link from 'next/link';
import { Button } from '@/components/ui/button';

function EmailConfirmationContent() {
  const searchParams = useSearchParams();
  const [message, setMessage] = useState('Confirming your email, please wait...');
  const [isSuccess, setIsSuccess] = useState(false);

  useEffect(() => {
    const userId = searchParams.get('userId');
    const token = searchParams.get('token');

    if (!userId || !token) {
      setMessage('Invalid confirmation link. Please check the URL and try again.');
      return;
    }

    const confirmEmail = async () => {
      try {
        await apiClient.get(`/accounts/confirm-email?userId=${userId}&token=${token}`);
        setMessage('Your email has been successfully confirmed! You can now log in.');
        setIsSuccess(true);
      } catch (err) {
        console.error("Email confirmation error:", err);
        setMessage('Failed to confirm your email. The link may be invalid or expired. Please try signing up again or contact support.');
        setIsSuccess(false);
      }
    };

    confirmEmail();
  }, [searchParams]);

  return (
    <div className="w-full min-h-screen flex items-center justify-center bg-gray-50">
      <div className="max-w-md w-full bg-white p-8 rounded-lg shadow-md text-center">
        <h1 className="text-2xl font-bold mb-4">Email Confirmation</h1>
        <p className={`text-lg ${isSuccess ? 'text-green-600' : 'text-red-600'}`}>
          {message}
        </p>
        {isSuccess && (
          <Button asChild className="mt-6">
            <Link href="/app/login">Go to Login</Link>
          </Button>
        )}
      </div>
    </div>
  );
}

export default function EmailConfirmationPage() {
    return (
        <Suspense fallback={<div>Loading...</div>}>
            <EmailConfirmationContent />
        </Suspense>
    );
}
