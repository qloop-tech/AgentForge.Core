# OpenWA integration in AgentForge

This folder contains the embedded OpenWA API and dashboard source used by AgentForge. It is part of the platform runtime rather than a standalone product fork, so the repository treats it as the WhatsApp transport layer behind the broader agent experience.

## Role in the solution

- AgentForge.AppHost starts the OpenWA API and dashboard as Aspire resources.
- AgentForge.WebApi receives signed webhook events from OpenWA and sends outbound replies through the OpenWA API.
- The dashboard provides session management, QR pairing, webhook configuration, and basic infrastructure visibility.

## Where to look

- Platform flow and boundaries: [../../docs/Architecture.md](../../docs/Architecture.md)
- Dashboard source: [dashboard/README.md](dashboard/README.md)

## Working with this source

The OpenWA code under this folder remains part of the AgentForge solution. If you change the gateway behavior, keep the work scoped to this source tree and make sure the surrounding platform flow still works end to end.

## Local development

```bash
cd src/OpenWA
npm install
npm run dev
```

The API and dashboard are expected to run through the AgentForge AppHost workflow, with the platform wiring provided by Aspire and the surrounding .NET services.
