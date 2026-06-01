ARG OPENWA_VERSION=0.1.6

FROM alpine:3.22 AS source
ARG OPENWA_VERSION
RUN apk add --no-cache curl tar
WORKDIR /src
RUN case "$OPENWA_VERSION" in \
      main) archive_url="https://codeload.github.com/rmyndharis/OpenWA/tar.gz/refs/heads/main" ;; \
      v*) archive_url="https://codeload.github.com/rmyndharis/OpenWA/tar.gz/refs/tags/${OPENWA_VERSION}" ;; \
      *) archive_url="https://codeload.github.com/rmyndharis/OpenWA/tar.gz/refs/tags/v${OPENWA_VERSION}" ;; \
    esac \
 && curl -fsSL "$archive_url" -o openwa.tar.gz \
 && tar -xzf openwa.tar.gz -C /src --strip-components=1

FROM node:22-alpine AS build
WORKDIR /app
COPY --from=source /src/package*.json ./
COPY --from=source /src/dashboard/package*.json ./dashboard/
RUN npm ci --ignore-scripts
RUN npm install --prefix dashboard
COPY --from=source /src/ ./
RUN node <<'NODE'
const fs = require('fs');

const path = '/app/dashboard/src/services/api.ts';
let source = fs.readFileSync(path, 'utf8');

source = source.replace(
  `// =============================================================================\n// Session API\n// =============================================================================\n`,
  `function normalizeWebhookEvents(events: Webhook['events'] | string): string[] {\n  if (Array.isArray(events)) {\n    return events;\n  }\n\n  if (typeof events === 'string') {\n    try {\n      const parsed = JSON.parse(events);\n      return Array.isArray(parsed) ? parsed.filter((event): event is string => typeof event === 'string') : [];\n    } catch {\n      return [];\n    }\n  }\n\n  return [];\n}\n\nfunction normalizeWebhook(webhook: Webhook): Webhook {\n  return {\n    ...webhook,\n    events: normalizeWebhookEvents(webhook.events),\n  };\n}\n\nfunction normalizeWebhooks(webhooks: Webhook[]): Webhook[] {\n  return webhooks.map(normalizeWebhook);\n}\n\n// =============================================================================\n// Session API\n// =============================================================================\n`
);

source = source.replace(
  `export const webhookApi = {\n  listBySession: (sessionId: string) => request<Webhook[]>(\`/sessions/\${sessionId}/webhooks\`),\n  listAll: () => request<Webhook[]>('/webhooks'),\n  get: (sessionId: string, id: string) => request<Webhook>(\`/sessions/\${sessionId}/webhooks/\${id}\`),\n  create: (sessionId: string, data: { url: string; events: string[] }) =>\n    request<Webhook>(\`/sessions/\${sessionId}/webhooks\`, {\n      method: 'POST',\n      body: JSON.stringify(data),\n    }),\n  update: (sessionId: string, id: string, data: Partial<Webhook>) =>\n    request<Webhook>(\`/sessions/\${sessionId}/webhooks/\${id}\`, {\n      method: 'PUT',\n      body: JSON.stringify(data),\n    }),\n  delete: (sessionId: string, id: string) =>\n    request<void>(\`/sessions/\${sessionId}/webhooks/\${id}\`, { method: 'DELETE' }),\n  test: (sessionId: string, id: string) =>\n    request<{ success: boolean; statusCode?: number; error?: string }>(\`/sessions/\${sessionId}/webhooks/\${id}/test\`, {\n      method: 'POST',\n    }),\n};`,
  `export const webhookApi = {\n  listBySession: async (sessionId: string) => normalizeWebhooks(await request<Webhook[]>(\`/sessions/\${sessionId}/webhooks\`)),\n  listAll: async () => normalizeWebhooks(await request<Webhook[]>('/webhooks')),\n  get: async (sessionId: string, id: string) => normalizeWebhook(await request<Webhook>(\`/sessions/\${sessionId}/webhooks/\${id}\`)),\n  create: async (sessionId: string, data: { url: string; events: string[] }) =>\n    normalizeWebhook(\n      await request<Webhook>(\`/sessions/\${sessionId}/webhooks\`, {\n        method: 'POST',\n        body: JSON.stringify(data),\n      }),\n    ),\n  update: async (sessionId: string, id: string, data: Partial<Webhook>) =>\n    normalizeWebhook(\n      await request<Webhook>(\`/sessions/\${sessionId}/webhooks/\${id}\`, {\n        method: 'PUT',\n        body: JSON.stringify(data),\n      }),\n    ),\n  delete: (sessionId: string, id: string) =>\n    request<void>(\`/sessions/\${sessionId}/webhooks/\${id}\`, { method: 'DELETE' }),\n  test: (sessionId: string, id: string) =>\n    request<{ success: boolean; statusCode?: number; error?: string }>(\`/sessions/\${sessionId}/webhooks/\${id}/test\`, {\n      method: 'POST',\n    }),\n};`
);

source = source.replace(
  `webhooks.map(webhook => (`,
  `webhooks.map((webhook: Webhook) => (`
);

fs.writeFileSync(path, source);
NODE
RUN npm run dashboard:build

FROM nginx:1.29-alpine
COPY --from=build /app/dashboard/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
