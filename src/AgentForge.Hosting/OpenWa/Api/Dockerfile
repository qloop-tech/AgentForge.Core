FROM alpine:3.22 AS source
ARG OPENWA_VERSION=0.1.6
RUN apk add --no-cache curl tar
WORKDIR /src
RUN case "$OPENWA_VERSION" in \
      main) archive_url="https://codeload.github.com/rmyndharis/OpenWA/tar.gz/refs/heads/main" ;; \
      v*) archive_url="https://codeload.github.com/rmyndharis/OpenWA/tar.gz/refs/tags/${OPENWA_VERSION}" ;; \
      *) archive_url="https://codeload.github.com/rmyndharis/OpenWA/tar.gz/refs/tags/v${OPENWA_VERSION}" ;; \
    esac \
 && curl -fsSL "$archive_url" -o openwa.tar.gz \
 && tar -xzf openwa.tar.gz -C /src --strip-components=1

FROM node:22-slim AS builder
WORKDIR /app
COPY --from=source /src/package*.json ./
COPY --from=source /src/dashboard/package*.json ./dashboard/
RUN apt-get update && apt-get install -y python3 make g++ && rm -rf /var/lib/apt/lists/*
RUN npm ci --ignore-scripts
RUN npm install --prefix dashboard
COPY --from=source /src/ ./
RUN npm run build

FROM node:22-slim AS production
RUN apt-get update && apt-get install -y \
    chromium \
    fonts-liberation \
    libappindicator3-1 \
    libasound2 \
    libatk-bridge2.0-0 \
    libatk1.0-0 \
    libcups2 \
    libdbus-1-3 \
    libdrm2 \
    libgbm1 \
    libgtk-3-0 \
    libnspr4 \
    libnss3 \
    libx11-xcb1 \
    libxcomposite1 \
    libxdamage1 \
    libxrandr2 \
    xdg-utils \
    dumb-init \
    && rm -rf /var/lib/apt/lists/*

ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true

WORKDIR /app
COPY --from=source /src/package*.json ./
RUN npm ci --omit=dev && npm cache clean --force
COPY --from=builder /app/dist ./dist
RUN mkdir -p ./data/sessions ./data/media
EXPOSE 2785
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD node -e "require('http').get('http://localhost:2785/api/health', (r) => process.exit(r.statusCode === 200 ? 0 : 1))"
ENTRYPOINT ["dumb-init", "--"]
CMD ["node", "dist/main"]
