import { MigrationInterface, QueryRunner } from 'typeorm';

export class WidenApiKeyPrefix1770113300000 implements MigrationInterface {
  name = 'WidenApiKeyPrefix1770113300000';

  public async up(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(`ALTER TABLE "api_keys" ALTER COLUMN "keyPrefix" TYPE varchar(12)`);
  }

  public async down(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(`ALTER TABLE "api_keys" ALTER COLUMN "keyPrefix" TYPE varchar(8)`);
  }
}
