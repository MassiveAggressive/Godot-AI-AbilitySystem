# Mimari — AbilitySystem

Sistem ayrıntıları ve kararları: `Docs/Systems/<Sistem>.md`. Burada yalnızca harita var.

## Sınıflar
| Sınıf | GAS karşılığı | Durum |
|---|---|---|
| `AttributeValue`, `AttributeSet`, `AttributeId`, `AttributeRegistry`, `AttributeContainer`, `Aggregator`, `ModifierOp`, `ModifierEvaluationChannel` | FGameplayAttributeData, UAttributeSet, FAggregator… | Uygulandı (`Attribute.md`) |
| `ModifierSource`, `ModifierEventData` | FGameplayEffectModCallbackData | Uygulandı (`Effect.md` Faz 0) |
| `EffectData`, `Modifier`, `Magnitude` (Resource'lar), `EffectSpec`, `EffectHandle`, `ActiveEffect`, `ActiveEffectContainer` | UGameplayEffect, FGameplayModifierInfo, FGameplayEffectSpec, FActiveGameplayEffect… | Faz 1–3 uygulandı (`Effect.md`) |
| `MagnitudeCalculation`, `ExecutionCalculation` (soyut Resource'lar), `CalculationContext`, `ExecutionOutput` | UGameplayModMagnitudeCalculation, UGameplayEffectExecutionCalculation | Uygulandı (`Effect.md` Faz 3) |
| `Ability` (soyut Resource), `AbilityHandle`, `AbilityContainer`, `ActivationFailure`, `AbilityEventData` | UGameplayAbility, FGameplayAbilitySpecHandle, FGameplayEventData | Faz 1–2 uygulandı (`Ability.md`) |
| `AbilityTask`, `WaitDelayTask`, `WaitEventTask`, `WaitTagTask`, `WaitInputReleaseTask` | UAbilityTask, UAbilityTask_Wait… | Faz 1–2 uygulandı (`Ability.md`) |
| Seviye: `Magnitude.LevelValues`/`LevelCurve`, `EffectSpec.Level`, `Ability.Level` (ayrı sınıf yok) | FScalableFloat, spec level | Uygulandı (`Level.md`) |
| Cooldown | Cooldown GE | Ayrı sınıf yok: `Ability.Cooldown` bir `EffectData` (HasDuration + granted tag) |
| `AbilitySystemComponent` | ASC | Uygulandı; alt sistemleri birleştirir, kendisi mantık taşımaz (`Ability.md`) |

## Bağımlılık Yönü
```
AbilitySystemComponent
   ├── Ability / AbilitySpec
   ├── ActiveEffectContainer / ActiveEffect / EffectSpec / EffectData / Modifier ──► Timers addon'u
   ├── AttributeContainer / AttributeSet / AttributeValue / Aggregator
   └── TagCountContainer / TagContainer (Tags addon'u)
```
- Alt sistemler ASC'yi bilmez. Effect katmanı Attribute katmanını kullanır, tersi yok.
- `Tags` ve `Timers` AbilitySystem'i bilmez.

## Genel Kararlar
- `Gameplay` ön eki yok; saf C# tipleri (Godot marshalling maliyeti); ağ (replication) kapsam dışı.
- Tag'ler ayrı `Tags` addon'unda; zaman yönetimi ayrı `Timers` addon'unda olacak.
