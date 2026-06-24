# OpenWA dashboard

This folder contains the Vite dashboard used by the embedded OpenWA gateway inside AgentForge. It is started by AgentForge.AppHost as the OpenWA dashboard resource and surfaces session, webhook, and infrastructure management for the WhatsApp transport layer.

## What it does

- Manage WhatsApp sessions and connection state
- Display QR codes for pairing
- Configure webhook endpoints and API keys
- Show basic infrastructure and health information

## Where to learn more

- Platform architecture: [../../../docs/Architecture.md](../../../docs/Architecture.md)
- Embedded gateway source: [../README.md](../README.md)

## Local development

```bash
cd src/OpenWA/dashboard
npm install
npm run dev
```

The dashboard is expected to run as part of the AgentForge AppHost flow, with API calls proxied to the embedded OpenWA backend.
