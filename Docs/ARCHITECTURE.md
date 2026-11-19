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
| `CueParameters`, `ICueNotify`, `CueEntry`, `CueLibrary` (Resource'lar), `CueContainer` | FGameplayCueParameters, IGameplayCueInterface/GameplayCueNotify, UGameplayCueManager | Effect cue'ları uygulandı (`Cue.md`) |
| `IAbilitySystem`, `TargetHit`, `TargetData`, `TargetQuery2D` (Resource), `TargetShape`, `TargetSort` | IAbilitySystemInterface, FGameplayAbilityTargetData(Handle), UE5 Targeting System (sade) | Faz 1–2 uygulandı (`Targeting.md`) |
| `WaitTargetDataTask`, `TargetConfirmation`, `TargetReticle2D` (Node2D); `AbilityContainer.InputConfirm`/`InputCancel` | UAbilityTask_WaitTargetData, AGameplayAbilityTargetActor_GroundTrace, AGameplayAbilityWorldReticle, LocalInputConfirm/Cancel | Faz 3 uygulandı (`Targeting.md`) |
| `TeamAttitude`, `TeamAttitudeMask`; `IAbilitySystem.Team`/`GetAttitude`, ASC `TeamName`/`Team`, `TargetQuery2D.Attitudes` | IGenericTeamAgentInterface, FGenericTeamId, ETeamAttitude | Faz 4 uygulandı (`Targeting.md`) |
| Cooldown | Cooldown GE | Ayrı sınıf yok: `Ability.Cooldown` bir `EffectData` (HasDuration + granted tag) |
| `AbilitySystemComponent` | ASC | Uygulandı; alt sistemleri birleştirir, kendisi mantık taşımaz (`Ability.md`) |

## Bağımlılık Yönü
```
AbilitySystemComponent
   ├── Ability / AbilitySpec
   ├── ActiveEffectContainer / ActiveEffect / EffectSpec / EffectData / Modifier ──► Timers addon'u
   ├── AttributeContainer / AttributeSet / AttributeValue / Aggregator
   ├── TagCountContainer / TagContainer (Tags addon'u)
   ├── CueContainer ──► CueLibrary / CueEntry, ActiveEffectContainer (olaylar)
   └── IAbilitySystem (uygular) ◄── Ability, TargetQuery2D ──► TargetData / TargetHit
Ability ──► WaitTargetDataTask ──► TargetReticle2D (yalnızca Godot), TargetQuery2D, TargetData
```
- Hedefin effect/tag'lerine `IAbilitySystem` ile ulaşılır; Ability ve hedefleme ASC'yi bilmez.
- Alt sistemler ASC'yi bilmez. Effect katmanı Attribute katmanını kullanır, tersi yok. Cue katmanı effect olaylarını dinler; effect katmanı cue'ları bilmez (`EffectData.Cues` yalnızca tag).
- `Tags` ve `Timers` AbilitySystem'i bilmez.

## Genel Kararlar
- `Gameplay` ön eki yok; saf C# tipleri (Godot marshalling maliyeti); ağ (replication) kapsam dışı.
- Tag'ler ayrı `Tags` addon'unda; zaman yönetimi ayrı `Timers` addon'unda olacak.
