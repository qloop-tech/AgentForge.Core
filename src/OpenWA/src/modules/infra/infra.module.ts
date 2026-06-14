import { Module } from '@nestjs/common';
import { InfraController } from './infra.controller';
import { EngineModule } from '../../engine/engine.module';

@Module({
  imports: [EngineModule],
  controllers: [InfraController],
})
export class InfraModule {}
