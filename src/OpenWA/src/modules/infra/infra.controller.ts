import { Controller, Get, Post, UploadedFile, UseInterceptors, StreamableFile } from '@nestjs/common';
import { FileInterceptor } from '@nestjs/platform-express';
import { ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { ConfigService } from '@nestjs/config';
import { InjectDataSource } from '@nestjs/typeorm';
import { DataSource } from 'typeorm';
import { Readable } from 'stream';
import { EngineFactory } from '../../engine/engine.factory';
import { CacheService } from '../../common/cache/cache.service';
import { StorageService } from '../../common/storage/storage.service';

interface InfraStatus {
  database: { connected: boolean; type: 'postgres'; host: string };
  redis: { enabled: boolean; connected: boolean; host: string; port: number };
  queue: {
    enabled: boolean;
    messages: { pending: number; completed: number; failed: number };
    webhooks: { pending: number; completed: number; failed: number };
  };
  storage: { type: 'local' | 's3'; path?: string; bucket?: string };
  engine: { type: string; headless: boolean; sessionDataPath: string; browserArgs: string };
}

@ApiTags('infrastructure')
@Controller('infra')
export class InfraController {
  constructor(
    private readonly configService: ConfigService,
    @InjectDataSource()
    private readonly dataSource: DataSource,
    private readonly engineFactory: EngineFactory,
    private readonly cacheService: CacheService,
    private readonly storageService: StorageService,
  ) {}

  @Get('status')
  @ApiOperation({ summary: 'Get Aspire-managed infrastructure status' })
  @ApiResponse({ status: 200, description: 'Infrastructure status' })
  async getStatus(): Promise<InfraStatus> {
    const redisHost = this.configService.get<string>('redis.host', 'localhost');
    const redisPort = this.configService.get<number>('redis.port', 6379);
    const storageType = this.configService.get<'local' | 's3'>('storage.type', 'local');
    const storagePath = this.configService.get<string>('storage.localPath', './data/media');
    const engineType = this.configService.get<string>('engine.type', 'whatsapp-web.js');
    const engineHeadless = this.configService.get<boolean>('engine.puppeteer.headless', true);
    const sessionDataPath = this.configService.get<string>('engine.sessionDataPath', './data/sessions');
    const browserArgs = (this.configService.get<string[]>('engine.puppeteer.args') || []).join(' ');

    return {
      database: {
        connected: this.dataSource.isInitialized,
        type: 'postgres',
        host: this.configService.get<string>('database.host', 'localhost'),
      },
      redis: {
        enabled: this.configService.get<boolean>('cache.enabled', false),
        connected: await this.cacheService.isAvailable(),
        host: redisHost,
        port: redisPort,
      },
      queue: {
        enabled: this.configService.get<boolean>('queue.enabled', false),
        messages: { pending: 0, completed: 0, failed: 0 },
        webhooks: { pending: 0, completed: 0, failed: 0 },
      },
      storage: {
        type: storageType,
        path: storageType === 'local' ? storagePath : undefined,
        bucket: storageType === 's3' ? this.configService.get<string>('storage.s3.bucket') : undefined,
      },
      engine: { type: engineType, headless: engineHeadless, sessionDataPath, browserArgs },
    };
  }

  @Get('engines')
  @ApiOperation({ summary: 'Get available WhatsApp engines' })
  @ApiResponse({ status: 200, description: 'List of available engines' })
  getEngines(): Array<{ id: string; name: string; enabled: boolean; features: string[] }> {
    return this.engineFactory.getAvailableEngines();
  }

  @Get('engines/current')
  @ApiOperation({ summary: 'Get current active engine' })
  @ApiResponse({ status: 200, description: 'Current engine info' })
  getCurrentEngine(): { engineType: string } {
    return { engineType: this.engineFactory.getCurrentEngine() };
  }

  @Get('storage/export')
  @ApiOperation({ summary: 'Export current storage as tar.gz' })
  async exportStorage(): Promise<StreamableFile> {
    const stream = await this.storageService.createExportStream();
    return new StreamableFile(stream, {
      type: 'application/gzip',
      disposition: `attachment; filename="openwa-storage-${Date.now()}.tar.gz"`,
    });
  }

  @Post('storage/import')
  @UseInterceptors(FileInterceptor('file'))
  @ApiOperation({ summary: 'Import storage from tar.gz' })
  async importStorage(@UploadedFile() file: { buffer: Buffer }): Promise<{ imported: number }> {
    const stream = Readable.from(file.buffer);
    return { imported: await this.storageService.importFromStream(stream) };
  }
}
