import { Injectable, NotFoundException, NotImplementedException } from '@nestjs/common';
import { SessionService } from '../session/session.service';
import type { Status, StatusResult, TextStatusOptions } from '../../engine/interfaces/whatsapp-engine.interface';

@Injectable()
export class StatusService {
  constructor(private readonly sessionService: SessionService) {}

  async getStatuses(sessionId: string): Promise<Status[]> {
    const engine = this.sessionService.getEngine(sessionId);
    if (!engine) {
      throw new NotFoundException(`Session ${sessionId} not found or not connected`);
    }
    throw new NotImplementedException('Status/stories are not implemented by the whatsapp-web.js adapter.');
  }

  async getContactStatus(sessionId: string, contactId: string): Promise<Status[]> {
    const engine = this.sessionService.getEngine(sessionId);
    if (!engine) {
      throw new NotFoundException(`Session ${sessionId} not found or not connected`);
    }
    throw new NotImplementedException('Status/stories are not implemented by the whatsapp-web.js adapter.');
  }

  async postTextStatus(sessionId: string, text: string, options?: TextStatusOptions): Promise<StatusResult> {
    const engine = this.sessionService.getEngine(sessionId);
    if (!engine) {
      throw new NotFoundException(`Session ${sessionId} not found or not connected`);
    }
    void text;
    void options;
    throw new NotImplementedException('Status/stories are not implemented by the whatsapp-web.js adapter.');
  }

  async postImageStatus(
    sessionId: string,
    media: { url?: string; base64?: string },
    caption?: string,
  ): Promise<StatusResult> {
    const engine = this.sessionService.getEngine(sessionId);
    if (!engine) {
      throw new NotFoundException(`Session ${sessionId} not found or not connected`);
    }
    void media;
    void caption;
    throw new NotImplementedException('Status/stories are not implemented by the whatsapp-web.js adapter.');
  }

  async postVideoStatus(
    sessionId: string,
    media: { url?: string; base64?: string },
    caption?: string,
  ): Promise<StatusResult> {
    const engine = this.sessionService.getEngine(sessionId);
    if (!engine) {
      throw new NotFoundException(`Session ${sessionId} not found or not connected`);
    }
    void media;
    void caption;
    throw new NotImplementedException('Status/stories are not implemented by the whatsapp-web.js adapter.');
  }

  async deleteStatus(sessionId: string, statusId: string): Promise<void> {
    const engine = this.sessionService.getEngine(sessionId);
    if (!engine) {
      throw new NotFoundException(`Session ${sessionId} not found or not connected`);
    }
    void statusId;
    throw new NotImplementedException('Status/stories are not implemented by the whatsapp-web.js adapter.');
  }
}
