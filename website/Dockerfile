# Build stage - build the Astro site
FROM node:22-alpine AS builder

WORKDIR /app/website

# Copy package files
COPY website/package*.json ./

# Install dependencies
RUN npm ci

# Copy source
COPY website/ ./
COPY Novus.Tests/Examples/homepage_example.c Novus.Tests/Examples/homepage_example.novus /app/Novus.Tests/Examples/
COPY vscode-novus/syntaxes/novus.tmLanguage.json /app/vscode-novus/syntaxes/

# Build the site
RUN npm run build

# Production stage - serve with nginx
FROM nginx:alpine

# Copy custom nginx config
COPY website/nginx.conf /etc/nginx/nginx.conf

# Copy built site from builder
COPY --from=builder /app/website/dist /usr/share/nginx/html

# Expose port 80
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
