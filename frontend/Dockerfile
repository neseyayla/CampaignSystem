# syntax=docker/dockerfile:1

# ── Build stage ───────────────────────────────────────────────
# Node builds the Angular app into static files. Not shipped in the final image.
FROM node:22-alpine AS build
WORKDIR /app

# Copy the manifests first and install; this layer is cached until dependencies change.
COPY package*.json ./
RUN npm ci

# Then the source, and produce the production bundle.
COPY . .
RUN npm run build

# ── Runtime stage ─────────────────────────────────────────────
# nginx serves the built static files — small and fast, no Node at runtime.
FROM nginx:alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
# The Angular application builder puts the browser bundle under dist/frontend/browser.
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html
EXPOSE 80
