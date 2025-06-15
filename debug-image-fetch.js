// Debug script to test image fetching from within the frontend container
// Run this with: node debug-image-fetch.js

const https = require('https');

function testFetch(url, userAgent = 'Node.js') {
  return new Promise((resolve, reject) => {
    const options = {
      headers: {
        'User-Agent': userAgent,
        'Accept': 'image/*,*/*',
        'Accept-Encoding': 'gzip, deflate, br',
        'Connection': 'keep-alive'
      }
    };

    console.log(`\n=== Testing ${url} with User-Agent: ${userAgent} ===`);
    
    const req = https.get(url, options, (res) => {
      console.log('Status:', res.statusCode);
      console.log('Content-Type:', res.headers['content-type']);
      console.log('Content-Length:', res.headers['content-length']);
      console.log('Headers:', JSON.stringify(res.headers, null, 2));
      
      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        console.log('Response body length:', data.length);
        console.log('First 200 chars:', data.substring(0, 200));
        resolve({
          statusCode: res.statusCode,
          headers: res.headers,
          bodyLength: data.length,
          isHtml: data.includes('<html') || data.includes('<!DOCTYPE')
        });
      });
    });

    req.on('error', reject);
  });
}

async function runTests() {
  const imageUrl = 'https://app.sagrafacile.it/media/some-image.png'; // Replace with actual image URL
  
  try {
    // Test with different User-Agent strings
    await testFetch(imageUrl, 'curl/7.68.0');
    await testFetch(imageUrl, 'Node.js');
    await testFetch(imageUrl, 'Next.js');
    await testFetch(imageUrl, 'Mozilla/5.0 (compatible; Next.js)');
    
    // Test the Next.js image optimization endpoint
    const nextImageUrl = `https://app.sagrafacile.it/_next/image?url=${encodeURIComponent(imageUrl)}&w=640&q=75`;
    await testFetch(nextImageUrl, 'Next.js');
    
  } catch (error) {
    console.error('Test failed:', error);
  }
}

runTests();
