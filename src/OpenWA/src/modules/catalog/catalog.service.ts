import { Injectable, NotFoundException, NotImplementedException } from '@nestjs/common';
import { SessionService } from '../session/session.service';
import type {
  Catalog,
  Product,
  PaginatedProducts,
  MessageResult,
} from '../../engine/interfaces/whatsapp-engine.interface';

@Injectable()
export class CatalogService {
  constructor(private readonly sessionService: SessionService) {}

  async getCatalog(sessionId: string): Promise<Catalog | null> {
    const engine = this.sessionService.getEngine(sessionId);
    if (!engine) {
      throw new NotFoundException(`Session ${sessionId} not found or not connected`);
    }
    throw new NotImplementedException('Catalog APIs are not implemented by the whatsapp-web.js adapter.');
  }

  async getProducts(sessionId: string, page = 1, limit = 20): Promise<PaginatedProducts> {
    const engine = this.sessionService.getEngine(sessionId);
    if (!engine) {
      throw new NotFoundException(`Session ${sessionId} not found or not connected`);
    }
    void page;
    void limit;
    throw new NotImplementedException('Catalog APIs are not implemented by the whatsapp-web.js adapter.');
  }

  async getProduct(sessionId: string, productId: string): Promise<Product | null> {
    const engine = this.sessionService.getEngine(sessionId);
    if (!engine) {
      throw new NotFoundException(`Session ${sessionId} not found or not connected`);
    }
    void productId;
    throw new NotImplementedException('Catalog APIs are not implemented by the whatsapp-web.js adapter.');
  }

  async sendProduct(sessionId: string, chatId: string, productId: string, body?: string): Promise<MessageResult> {
    const engine = this.sessionService.getEngine(sessionId);
    if (!engine) {
      throw new NotFoundException(`Session ${sessionId} not found or not connected`);
    }
    void chatId;
    void productId;
    void body;
    throw new NotImplementedException('Catalog APIs are not implemented by the whatsapp-web.js adapter.');
  }

  async sendCatalog(sessionId: string, chatId: string, body?: string): Promise<MessageResult> {
    const engine = this.sessionService.getEngine(sessionId);
    if (!engine) {
      throw new NotFoundException(`Session ${sessionId} not found or not connected`);
    }
    void chatId;
    void body;
    throw new NotImplementedException('Catalog APIs are not implemented by the whatsapp-web.js adapter.');
  }
}
