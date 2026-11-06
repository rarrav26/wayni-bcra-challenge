# Frontend: multi-stage build. Compiles the React/Vite app and serves the static assets with nginx.
# Production-style: the final image is a small static web server, not the Vite dev server.

# --- Build stage ---
FROM node:22-alpine AS build

WORKDIR /app

# Install dependencies first to leverage layer caching.
COPY package.json package-lock.json ./
RUN npm ci

# The API base URL is baked into the bundle at build time (Vite inlines VITE_* vars during build).
# Passed as a build arg; defaults to the local API port used by docker-compose.
ARG VITE_API_URL=http://localhost:8080
ENV VITE_API_URL=$VITE_API_URL

COPY . .
RUN npm run build

# --- Runtime stage ---
FROM nginx:1.27-alpine AS runtime

# SPA routing + sensible defaults.
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

# nginx runs in the foreground by default via its base image CMD.
