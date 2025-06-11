import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  allowedDevOrigins: ["http://192.168.1.219:3000", "https://192.168.1.219:3000","https://192.168.1.38"],
  images: {
    remotePatterns: [
      {
        protocol: 'https',
        hostname: '192.168.1.38',
        port: '7075',
        pathname: '/media/**',
      },{
        protocol: 'https',
        hostname: '192.168.1.237',
        port: '7075',
        pathname: '/media/**',
      },
    ],
  },
};

export default nextConfig;
