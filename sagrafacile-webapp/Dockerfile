# Stage 1: Build the Next.js application
FROM node:20-alpine AS builder
WORKDIR /app

# Copy package.json and package-lock.json (or yarn.lock)
COPY package.json ./
# If you use package-lock.json, uncomment the next line
COPY package-lock.json ./
# If you use yarn.lock, uncomment the next two lines
# COPY yarn.lock ./
# RUN yarn install --frozen-lockfile

# Install dependencies
RUN npm install

# Copy the rest of the application code
COPY . .

# Declare the build argument
ARG NEXT_PUBLIC_API_BASE_URL
# Set the environment variable for the build process
ENV NEXT_PUBLIC_API_BASE_URL=${NEXT_PUBLIC_API_BASE_URL}

# Build the Next.js application
RUN npm run build

# Stage 2: Production image
FROM node:20-alpine AS runner
WORKDIR /app

# Set environment to production
ENV NODE_ENV=production

# Copy built assets from the builder stage
COPY --from=builder /app/public ./public
COPY --from=builder /app/.next ./.next
COPY --from=builder /app/node_modules ./node_modules
COPY --from=builder /app/package.json ./package.json

# Expose port 3000
EXPOSE 3000

# Command to run the application
CMD ["npm", "start"]
