import { Injectable, BadRequestException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { SessionService } from '../session/session.service';
import { SendTextMessageDto, SendMediaMessageDto, MessageResponseDto } from './dto';
import { IWhatsAppEngine, MediaInput, MessageResult } from '../../engine/interfaces/whatsapp-engine.interface';
import { Message, MessageDirection, MessageStatus } from './entities/message.entity';
import { HookManager } from '../../core/hooks';

export interface GetMessagesOptions {
  chatId?: string;
  limit?: number;
  offset?: number;
}

type OutgoingMessageInput = {
  chatId: string;
  body?: string;
  type: string;
};

@Injectable()
export class MessageService {
  constructor(
    @InjectRepository(Message)
    private readonly messageRepository: Repository<Message>,
    private readonly sessionService: SessionService,
    private readonly hookManager: HookManager,
  ) {}

  async sendText(sessionId: string, dto: SendTextMessageDto): Promise<MessageResponseDto> {
    const finalDto = await this.applySendingHook<SendTextMessageDto>(sessionId, 'text', dto);
    return this.sendWithPersistence(sessionId, 'text', finalDto, {
      chatId: finalDto.chatId,
      body: finalDto.text,
      type: 'text',
    }, engine => engine.sendTextMessage(finalDto.chatId, finalDto.text));
  }

  async sendImage(sessionId: string, dto: SendMediaMessageDto): Promise<MessageResponseDto> {
    const finalDto = await this.applySendingHook<SendMediaMessageDto>(sessionId, 'image', dto);
    const media = this.buildMediaInput(finalDto);
    return this.sendWithPersistence(sessionId, 'image', finalDto, {
      chatId: finalDto.chatId,
      body: finalDto.caption || '',
      type: 'image',
    }, engine => engine.sendImageMessage(finalDto.chatId, media));
  }

  async sendVideo(sessionId: string, dto: SendMediaMessageDto): Promise<MessageResponseDto> {
    const finalDto = await this.applySendingHook<SendMediaMessageDto>(sessionId, 'video', dto);
    const media = this.buildMediaInput(finalDto);
    return this.sendWithPersistence(sessionId, 'video', finalDto, {
      chatId: finalDto.chatId,
      body: finalDto.caption || '',
      type: 'video',
    }, engine => engine.sendVideoMessage(finalDto.chatId, media));
  }

  async sendAudio(sessionId: string, dto: SendMediaMessageDto): Promise<MessageResponseDto> {
    const finalDto = await this.applySendingHook<SendMediaMessageDto>(sessionId, 'audio', dto);
    const media = this.buildMediaInput(finalDto);
    return this.sendWithPersistence(sessionId, 'audio', finalDto, {
      chatId: finalDto.chatId,
      type: 'audio',
    }, engine => engine.sendAudioMessage(finalDto.chatId, media));
  }

  async sendDocument(sessionId: string, dto: SendMediaMessageDto): Promise<MessageResponseDto> {
    const finalDto = await this.applySendingHook<SendMediaMessageDto>(sessionId, 'document', dto);
    const media = this.buildMediaInput(finalDto);
    return this.sendWithPersistence(sessionId, 'document', finalDto, {
      chatId: finalDto.chatId,
      body: finalDto.filename || '',
      type: 'document',
    }, engine => engine.sendDocumentMessage(finalDto.chatId, media));
  }

  /**
   * Get message history for a session
   */
  async getMessages(
    sessionId: string,
    options: GetMessagesOptions = {},
  ): Promise<{ messages: Message[]; total: number }> {
    const { chatId, limit = 50, offset = 0 } = options;

    const query = this.messageRepository
      .createQueryBuilder('message')
      .where('message.sessionId = :sessionId', { sessionId })
      .orderBy('message.createdAt', 'DESC')
      .skip(offset)
      .take(limit);

    if (chatId) {
      query.andWhere('message.chatId = :chatId', { chatId });
    }

    const [messages, total] = await query.getManyAndCount();
    return { messages, total };
  }

  // ========== Phase 3: Extended Messaging ==========

  async sendLocation(
    sessionId: string,
    dto: { chatId: string; latitude: number; longitude: number; description?: string; address?: string },
  ): Promise<MessageResponseDto> {
    const finalDto = await this.applySendingHook(sessionId, 'location', dto);
    return this.sendWithPersistence(sessionId, 'location', finalDto, {
      chatId: finalDto.chatId,
      body: finalDto.description || 'Location',
      type: 'location',
    }, engine =>
      engine.sendLocationMessage(finalDto.chatId, {
        latitude: finalDto.latitude,
        longitude: finalDto.longitude,
        description: finalDto.description,
        address: finalDto.address,
      }),
    );
  }

  async sendContact(
    sessionId: string,
    dto: { chatId: string; contactName: string; contactNumber: string },
  ): Promise<MessageResponseDto> {
    const finalDto = await this.applySendingHook(sessionId, 'contact', dto);
    return this.sendWithPersistence(sessionId, 'contact', finalDto, {
      chatId: finalDto.chatId,
      body: finalDto.contactName,
      type: 'contact',
    }, engine =>
      engine.sendContactMessage(finalDto.chatId, {
        name: finalDto.contactName,
        number: finalDto.contactNumber,
      }),
    );
  }

  async sendSticker(sessionId: string, dto: SendMediaMessageDto): Promise<MessageResponseDto> {
    const finalDto = await this.applySendingHook<SendMediaMessageDto>(sessionId, 'sticker', dto);
    const media = this.buildMediaInput(finalDto);
    return this.sendWithPersistence(sessionId, 'sticker', finalDto, {
      chatId: finalDto.chatId,
      type: 'sticker',
    }, engine => engine.sendStickerMessage(finalDto.chatId, media));
  }

  async reply(
    sessionId: string,
    dto: { chatId: string; quotedMessageId: string; text: string },
  ): Promise<MessageResponseDto> {
    const finalDto = await this.applySendingHook(sessionId, 'reply', dto);
    return this.sendWithPersistence(sessionId, 'reply', finalDto, {
      chatId: finalDto.chatId,
      body: finalDto.text,
      type: 'text',
    }, engine => engine.replyToMessage(finalDto.chatId, finalDto.quotedMessageId, finalDto.text));
  }

  async forward(
    sessionId: string,
    dto: { fromChatId: string; toChatId: string; messageId: string },
  ): Promise<MessageResponseDto> {
    const finalDto = await this.applySendingHook(sessionId, 'forward', dto);
    return this.sendWithPersistence(sessionId, 'forward', finalDto, {
      chatId: finalDto.toChatId,
      body: '[Forwarded]',
      type: 'forward',
    }, engine => engine.forwardMessage(finalDto.fromChatId, finalDto.toChatId, finalDto.messageId));
  }

  /**
   * Save incoming message (called from session webhook dispatch)
   */
  async saveIncomingMessage(sessionId: string, data: Partial<Message>): Promise<Message> {
    const message = this.messageRepository.create({
      ...data,
      sessionId,
      direction: MessageDirection.INCOMING,
    });
    return this.messageRepository.save(message);
  }

  private async applySendingHook<TInput extends object>(sessionId: string, type: string, input: TInput): Promise<TInput> {
    const { continue: shouldContinue, data } = await this.hookManager.execute(
      'message:sending',
      { sessionId, input, type },
      { sessionId, source: 'MessageService' },
    );

    if (!shouldContinue) {
      throw new BadRequestException('Message sending blocked by plugin');
    }

    return (data as { input?: TInput }).input ?? input;
  }

  private async sendWithPersistence<TInput extends object>(
    sessionId: string,
    type: string,
    input: TInput,
    outgoing: OutgoingMessageInput,
    send: (engine: IWhatsAppEngine) => Promise<MessageResult>,
  ): Promise<MessageResponseDto> {
    const engine = this.getEngine(sessionId);
    const message = await this.saveOutgoingMessage(sessionId, outgoing);

    try {
      const result = await send(engine);

      message.waMessageId = result.id;
      message.status = MessageStatus.SENT;
      message.timestamp = result.timestamp;
      await this.messageRepository.save(message);

      await this.hookManager.execute('message:sent', { sessionId, result, input, type }, {
        sessionId,
        source: 'MessageService',
      });

      return {
        messageId: result.id,
        timestamp: result.timestamp,
      };
    } catch (error) {
      message.status = MessageStatus.FAILED;
      await this.messageRepository.save(message);

      await this.hookManager.execute(
        'message:failed',
        { sessionId, type, error: error instanceof Error ? error.message : String(error), input },
        { sessionId, source: 'MessageService' },
      );

      throw error;
    }
  }

  /**
   * Save outgoing message to database.
   * When called before sending, creates a record with PENDING status.
   */
  private async saveOutgoingMessage(
    sessionId: string,
    data: {
      waMessageId?: string;
      chatId: string;
      body?: string;
      type: string;
      timestamp?: number;
      status?: MessageStatus;
    },
  ): Promise<Message> {
    const session = await this.sessionService.findOne(sessionId);
    const message = this.messageRepository.create({
      sessionId,
      waMessageId: data.waMessageId,
      chatId: data.chatId,
      from: session?.phone || 'me',
      to: data.chatId,
      body: data.body,
      type: data.type,
      direction: MessageDirection.OUTGOING,
      timestamp: data.timestamp,
      status: data.status ?? MessageStatus.PENDING,
    });
    return this.messageRepository.save(message);
  }

  // ========== Phase 3: Reactions ==========

  async reactToMessage(sessionId: string, dto: { chatId: string; messageId: string; emoji: string }): Promise<void> {
    const engine = this.getEngine(sessionId);
    await engine.reactToMessage(dto.chatId, dto.messageId, dto.emoji);
  }

  async getMessageReactions(sessionId: string, chatId: string, messageId: string) {
    const engine = this.getEngine(sessionId);
    return engine.getMessageReactions(chatId, messageId);
  }

  // ========== Delete Message ==========

  async deleteMessage(
    sessionId: string,
    dto: { chatId: string; messageId: string; forEveryone?: boolean },
  ): Promise<void> {
    const engine = this.getEngine(sessionId);
    await engine.deleteMessage(dto.chatId, dto.messageId, dto.forEveryone ?? true);
  }

  private getEngine(sessionId: string) {
    const engine = this.sessionService.getEngine(sessionId);
    if (!engine) {
      throw new BadRequestException(`Session '${sessionId}' is not active. Start the session first.`);
    }
    return engine;
  }

  private buildMediaInput(dto: SendMediaMessageDto): MediaInput {
    if (!dto.url && !dto.base64) {
      throw new BadRequestException('Either url or base64 must be provided');
    }

    if (dto.base64 && !dto.mimetype) {
      throw new BadRequestException('mimetype is required when using base64 data');
    }

    return {
      mimetype: dto.mimetype || 'application/octet-stream',
      data: dto.url || dto.base64!,
      filename: dto.filename,
      caption: dto.caption,
    };
  }
}
