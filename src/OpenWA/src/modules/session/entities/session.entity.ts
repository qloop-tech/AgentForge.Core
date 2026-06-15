import { Entity, Column, PrimaryGeneratedColumn, CreateDateColumn, UpdateDateColumn } from 'typeorm';
import { jsonColumnType, dateColumnType } from '../../../common/utils/column-types';

export enum SessionStatus {
  CREATED = 'created',
  INITIALIZING = 'initializing',
  QR_READY = 'qr_ready',
  AUTHENTICATING = 'authenticating',
  READY = 'ready',
  DISCONNECTED = 'disconnected',
  FAILED = 'failed',
}

@Entity('sessions')
export class Session {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @Column({ type: 'varchar', length: 100, unique: true })
  name: string;

  @Column({
    type: 'varchar',
    length: 50,
    default: SessionStatus.CREATED,
  })
  status: SessionStatus;

  @Column({ type: 'varchar', length: 20, nullable: true })
  phone: string | null;

  @Column({ type: 'varchar', length: 100, nullable: true })
  pushName: string | null;

  @Column({ type: jsonColumnType(), default: '{}' })
  config: Record<string, unknown>;

  // Phase 3: Proxy per session
  @Column({ type: 'varchar', length: 255, nullable: true })
  proxyUrl: string | null;

  @Column({ type: 'varchar', length: 10, nullable: true })
  proxyType: 'http' | 'https' | 'socks4' | 'socks5' | null;

  @Column({ type: dateColumnType(), nullable: true })
  connectedAt: Date | null;

  @Column({ type: dateColumnType(), nullable: true })
  lastActiveAt: Date | null;

  @CreateDateColumn({ type: dateColumnType() })
  createdAt: Date;

  @UpdateDateColumn({ type: dateColumnType() })
  updatedAt: Date;
}
