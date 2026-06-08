# Journaling TopN

World records: 81,544

## Record Operations

| Operation | Count | Per tick |
|---|---:|---:|
| GetBufferRW | 60,684 | 6,742.7 |
| GetComponentDataRW | 20,210 | 2,245.6 |
| EnableComponent | 650 | 72.2 |

## Systems

| Operation | System | Count | Per tick |
|---|---|---:|---:|
| GetBufferRW | GAS.Runtime.OwnerLocalInstantCommandFramePrepareSystem | 9,000 | 1,000.0 |
| GetBufferRW | GAS.Runtime.ActiveEffectOwnerLocalMutationFramePrepareSystem | 9,000 | 1,000.0 |
| GetBufferRW | GAS.Runtime.GASActiveEffectPreTickSystem | 8,600 | 955.6 |
| GetBufferRW | GAS.Runtime.GASAttributeModifierDeltaApplySystem | 5,400 | 600.0 |
| GetBufferRW | GAS.Runtime.ASCCommandBufferResolveSystem | 4,550 | 505.6 |
| GetComponentDataRW | GAS.Runtime.GASActiveEffectPreTickSystem | 4,209 | 467.7 |
| GetBufferRW | GAS.Runtime.GASActiveEffectMutationApplySystem | 3,750 | 416.7 |
| GetComponentDataRW | GAS.AutoChessDemo.AutoChessExecuteDamageCalculationSystem | 3,609 | 401.0 |

## Components

| Operation | Component | Count | Per tick |
|---|---|---:|---:|
| GetBufferRW | GAS.Runtime.OwnerLocalGameplayFactBuffer | 11,250 | 1,250.0 |
| GetComponentDataRW | GAS.Runtime.GEEffectCommandStreamComponent | 10,555 | 1,172.8 |
| GetBufferRW | GAS.Runtime.AttributeValueBuffer | 7,050 | 783.3 |
| GetBufferRW | GAS.Runtime.GEEffectCommandBuffer | 4,650 | 516.7 |
| GetBufferRW | GAS.Runtime.ActiveEffectMutationBuffer | 4,000 | 444.4 |
| GetComponentDataRW | GAS.Runtime.PendingAttributeModifierComponent | 3,600 | 400.0 |
| GetBufferRW | GAS.Runtime.AttributeModifierBuffer | 3,600 | 400.0 |
| GetBufferRW | GAS.Runtime.GESetByCallerValueBuffer | 3,350 | 372.2 |
