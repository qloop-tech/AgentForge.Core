import { Test, TestingModule } from '@nestjs/testing';
import { getRepositoryToken } from '@nestjs/typeorm';
import { getQueueToken } from '@nestjs/bullmq';
import { Repository } from 'typeorm';
import { NotFoundException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as crypto from 'crypto';
import { WebhookService, WebhookPayload } from './webhook.service';
import { Webhook } from './entities/webhook.entity';
import { HookManager } from '../../core/hooks';
import { QUEUE_NAMES } from '../queue/queue-names';
import { Session } from '../session/entities/session.entity';

function createMockWebhook(overrides: Partial<Webhook> = {}): Webhook {
  return {
    id: 'wh-uuid-1',
    sessionId: 'sess-1',
    url: 'https://example.com/webhook',
    events: ['message.received'],
    secret: null,
    headers: {},
    active: true,
    retryCount: 3,
    lastTriggeredAt: null,
    createdAt: new Date(),
    updatedAt: new Date(),
    session: undefined as unknown as Session,
    ...overrides,
  };
}

describe('WebhookService', () => {
  let service: WebhookService;
  let repository: jest.Mocked<Partial<Repository<Webhook>>>;
  let configService: jest.Mocked<Partial<ConfigService>>;
  let hookManager: jest.Mocked<Partial<HookManager>>;
  let webhookQueue: jest.Mocked<Record<string, jest.Mock>>;

  beforeEach(async () => {
    repository = {
      find: jest.fn(),
      findOne: jest.fn(),
      create: jest.fn(),
      save: jest.fn(),
      remove: jest.fn(),
      update: jest.fn(),
    };

    configService = {
      get: jest.fn().mockImplementation(<T>(key: string, def?: T): T | boolean | number => {
        if (key === 'webhook.retryDelay') return 100;
        if (key === 'webhook.timeout') return 10000;
        return def as T;
      }),
    };

    hookManager = {
      execute: jest.fn().mockResolvedValue({
        continue: true,
        data: { sessionId: 'sess-1', event: 'message.received', payload: {} },
      }),
    };

    webhookQueue = {
      add: jest.fn().mockResolvedValue(undefined),
    };

    const module: TestingModule = await Test.createTestingModule({
      providers: [
        WebhookService,
        { provide: getRepositoryToken(Webhook), useValue: repository },
        { provide: ConfigService, useValue: configService },
        { provide: HookManager, useValue: hookManager },
        { provide: getQueueToken(QUEUE_NAMES.WEBHOOK), useValue: webhookQueue },
      ],
    }).compile();

    service = module.get<WebhookService>(WebhookService);
  });

  // ── create ────────────────────────────────────────────────────────

  describe('create', () => {
    it('should create a webhook with default events', async () => {
      const webhook = createMockWebhook();
      (repository.create as jest.Mock).mockReturnValue(webhook);
      (repository.save as jest.Mock).mockResolvedValue(webhook);

      const result = await service.create('sess-1', {
        url: 'https://example.com/webhook',
      });

      expect(result.sessionId).toBe('sess-1');
      expect(repository.create).toHaveBeenCalledWith(
        expect.objectContaining({
          sessionId: 'sess-1',
          events: ['message.received'],
        }),
      );
    });

    it('should create webhook with custom events and secret', async () => {
      const webhook = createMockWebhook({
        events: ['*'],
        secret: 'my-secret',
      });
      (repository.create as jest.Mock).mockReturnValue(webhook);
      (repository.save as jest.Mock).mockResolvedValue(webhook);

      await service.create('sess-1', {
        url: 'https://example.com/webhook',
        events: ['*'],
        secret: 'my-secret',
      });

      expect(repository.create).toHaveBeenCalledWith(
        expect.objectContaining({
          events: ['*'],
          secret: 'my-secret',
        }),
      );
    });
  });

  // ── findBySession / findAll / findOne ──────────────────────────────

  describe('findBySession', () => {
    it('should return webhooks for a session', async () => {
      const webhooks = [createMockWebhook()];
      (repository.find as jest.Mock).mockResolvedValue(webhooks);

      const result = await service.findBySession('sess-1');

      expect(result).toHaveLength(1);
      expect(repository.find).toHaveBeenCalledWith(expect.objectContaining({ where: { sessionId: 'sess-1' } }));
    });
  });

  describe('findAll', () => {
    it('should return all webhooks ordered by createdAt DESC', async () => {
      (repository.find as jest.Mock).mockResolvedValue([]);

      await service.findAll();

      expect(repository.find).toHaveBeenCalledWith({ order: { createdAt: 'DESC' } });
    });
  });

  describe('findOne', () => {
    it('should return webhook by id', async () => {
      const webhook = createMockWebhook();
      (repository.findOne as jest.Mock).mockResolvedValue(webhook);

      const result = await service.findOne('wh-uuid-1');
      expect(result.id).toBe('wh-uuid-1');
    });

    it('should throw NotFoundException if not found', async () => {
      (repository.findOne as jest.Mock).mockResolvedValue(null);

      await expect(service.findOne('nonexistent')).rejects.toThrow(NotFoundException);
    });
  });

  // ── update ────────────────────────────────────────────────────────

  describe('update', () => {
    it('should update only provided fields', async () => {
      const webhook = createMockWebhook();
      (repository.findOne as jest.Mock).mockResolvedValue(webhook);
      (repository.save as jest.Mock).mockImplementation(w => Promise.resolve(w));

      const result = await service.update('wh-uuid-1', { url: 'https://new-url.com/hook' });

      expect(result.url).toBe('https://new-url.com/hook');
      expect(result.events).toEqual(['message.received']); // unchanged
    });
  });

  // ── delete ────────────────────────────────────────────────────────

  describe('delete', () => {
    it('should remove the webhook', async () => {
      const webhook = createMockWebhook();
      (repository.findOne as jest.Mock).mockResolvedValue(webhook);
      (repository.remove as jest.Mock).mockResolvedValue(webhook);

      await service.delete('wh-uuid-1');

      expect(repository.remove).toHaveBeenCalledWith(webhook);
    });
  });

  // ── dispatch ──────────────────────────────────────────────────────

  describe('dispatch', () => {
    it('should queue webhooks matching the event', async () => {
      const webhook = createMockWebhook({ events: ['message.received'] });
      (repository.find as jest.Mock).mockResolvedValue([webhook]);
      (repository.update as jest.Mock).mockResolvedValue({ affected: 1 });

      const mockPayload: WebhookPayload = {
        event: 'message.received',
        timestamp: new Date().toISOString(),
        sessionId: 'sess-1',
        idempotencyKey: 'test-key',
        deliveryId: 'test-delivery',
        data: { from: '628123456789@c.us' },
      };
      (hookManager.execute as jest.Mock).mockResolvedValue({
        continue: true,
        data: {
          sessionId: 'sess-1',
          event: 'message.received',
          payload: mockPayload,
        },
      });

      await service.dispatch('sess-1', 'message.received', { from: '628123456789@c.us' });

      expect(webhookQueue.add).toHaveBeenCalledWith(
        expect.stringContaining('webhook-'),
        expect.objectContaining({
          webhookId: 'wh-uuid-1',
          url: 'https://example.com/webhook',
          event: 'message.received',
          payload: mockPayload,
        }),
        expect.objectContaining({
          attempts: 3,
          backoff: expect.objectContaining({ type: 'exponential' }),
        }),
      );
    });

    it('should NOT queue webhooks that do not match the event', async () => {
      const webhook = createMockWebhook({ events: ['message.received'] });
      (repository.find as jest.Mock).mockResolvedValue([webhook]);

      await service.dispatch('sess-1', 'session.ready', { phone: '628123456789' });

      expect(webhookQueue.add).not.toHaveBeenCalled();
    });

    it('should queue webhooks with wildcard (*) event filter', async () => {
      const webhook = createMockWebhook({ events: ['*'] });
      (repository.find as jest.Mock).mockResolvedValue([webhook]);
      (repository.update as jest.Mock).mockResolvedValue({ affected: 1 });

      const wildcardPayload: WebhookPayload = {
        event: 'anything.goes',
        timestamp: '',
        sessionId: 'sess-1',
        idempotencyKey: '',
        deliveryId: '',
        data: {},
      };
      (hookManager.execute as jest.Mock).mockResolvedValue({
        continue: true,
        data: {
          sessionId: 'sess-1',
          event: 'anything.goes',
          payload: wildcardPayload,
        },
      });

      await service.dispatch('sess-1', 'anything.goes', {});

      expect(webhookQueue.add).toHaveBeenCalledWith(
        expect.stringContaining('webhook-'),
        expect.objectContaining({
          webhookId: 'wh-uuid-1',
          event: 'anything.goes',
          payload: wildcardPayload,
        }),
        expect.any(Object),
      );
    });

    it('should skip queue when plugin cancels via hook', async () => {
      const webhook = createMockWebhook({ events: ['message.received'] });
      (repository.find as jest.Mock).mockResolvedValue([webhook]);

      (hookManager.execute as jest.Mock).mockResolvedValue({ continue: false, data: {} });

      await service.dispatch('sess-1', 'message.received', {});

      expect(webhookQueue.add).not.toHaveBeenCalled();
    });
  });

  // ── generateSignature (via dispatch) ──────────────────────────────

  describe('generateSignature', () => {
    it('should produce valid HMAC-SHA256 signature', async () => {
      const webhook = createMockWebhook({
        events: ['message.received'],
        secret: 'test-secret-123',
      });
      (repository.find as jest.Mock).mockResolvedValue([webhook]);
      (repository.update as jest.Mock).mockResolvedValue({ affected: 1 });

      const sigPayload: WebhookPayload = {
        event: 'message.received',
        data: {},
        timestamp: '',
        sessionId: 'sess-1',
        idempotencyKey: 'k',
        deliveryId: 'd',
      };
      (hookManager.execute as jest.Mock).mockResolvedValue({
        continue: true,
        data: {
          sessionId: 'sess-1',
          event: 'message.received',
          payload: sigPayload,
        },
      });

      await service.dispatch('sess-1', 'message.received', {});

      const [, jobData] = webhookQueue.add.mock.calls[0] as [
        string,
        { headers: Record<string, string> },
        unknown,
      ];
      const capturedHeaders = jobData.headers;

      // Verify signature format
      expect(capturedHeaders['X-OpenWA-Signature']).toMatch(/^sha256=[a-f0-9]{64}$/);

      // Verify signature correctness
      const body = JSON.stringify({
        event: 'message.received',
        data: {},
        timestamp: '',
        sessionId: 'sess-1',
        idempotencyKey: 'k',
        deliveryId: 'd',
      });
      const expected = `sha256=${crypto.createHmac('sha256', 'test-secret-123').update(body).digest('hex')}`;
      expect(capturedHeaders['X-OpenWA-Signature']).toBe(expected);
    });
  });
});
