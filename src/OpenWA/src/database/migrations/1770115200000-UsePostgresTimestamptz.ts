import { MigrationInterface, QueryRunner } from 'typeorm';

export class UsePostgresTimestamptz1770115200000 implements MigrationInterface {
  name = 'UsePostgresTimestamptz1770115200000';

  private readonly columns: Array<[table: string, column: string]> = [
    ['sessions', 'connectedAt'],
    ['sessions', 'lastActiveAt'],
    ['sessions', 'createdAt'],
    ['sessions', 'updatedAt'],
    ['webhooks', 'lastTriggeredAt'],
    ['webhooks', 'createdAt'],
    ['webhooks', 'updatedAt'],
    ['messages', 'createdAt'],
    ['message_batches', 'created_at'],
    ['message_batches', 'updated_at'],
    ['message_batches', 'started_at'],
    ['message_batches', 'completed_at'],
    ['api_keys', 'expiresAt'],
    ['api_keys', 'lastUsedAt'],
    ['api_keys', 'createdAt'],
    ['api_keys', 'updatedAt'],
    ['audit_logs', 'createdAt'],
  ];

  public async up(queryRunner: QueryRunner): Promise<void> {
    await this.alterColumns(queryRunner, 'timestamptz');
  }

  public async down(queryRunner: QueryRunner): Promise<void> {
    await this.alterColumns(queryRunner, 'timestamp');
  }

  private async alterColumns(queryRunner: QueryRunner, type: 'timestamp' | 'timestamptz'): Promise<void> {
    for (const [table, column] of this.columns) {
      await queryRunner.query(`ALTER TABLE "${table}" ALTER COLUMN "${column}" TYPE ${type} USING "${column}"`);
    }
  }
}
