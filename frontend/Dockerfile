# Frontend dev server (React + Vite) for docker-compose.
# Serves the Vite dev server; simple and sufficient for the challenge/evaluation environment.
# For production, this would instead be a multi-stage build producing static assets served by nginx.
FROM node:22-alpine

WORKDIR /app

# Install dependencies first to leverage layer caching.
COPY package.json package-lock.json ./
RUN npm ci

# Copy the rest of the source.
COPY . .

EXPOSE 5173

# --host exposes the dev server outside the container (binds 0.0.0.0).
CMD ["npm", "run", "dev", "--", "--host"]
