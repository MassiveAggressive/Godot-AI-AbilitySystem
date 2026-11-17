# Mimari — AbilitySystem

Sistem ayrıntıları ve kararları: `Docs/Systems/<Sistem>.md`. Burada yalnızca harita var.

## Sınıflar
| Sınıf | GAS karşılığı | Durum |
|---|---|---|
| `AttributeValue`, `AttributeSet`, `AttributeId`, `AttributeRegistry`, `AttributeContainer`, `Aggregator`, `ModifierOp`, `ModifierEvaluationChannel` | FGameplayAttributeData, UAttributeSet, FAggregator… | Uygulandı (`Attribute.md`) |
| `ModifierSource`, `ModifierEventData` | FGameplayEffectModCallbackData | Uygulandı (`Effect.md` Faz 0) |
| `EffectData`, `Modifier`, `Magnitude` (Resource'lar), `EffectSpec`, `EffectHandle`, `ActiveEffect`, `ActiveEffectContainer` | UGameplayEffect, FGameplayModifierInfo, FGameplayEffectSpec, FActiveGameplayEffect… | Faz 1 uygulandı; Faz 2–3 taslak (`Effect.md`) |
| `AbilityData` (Resource), `Ability`, `AbilitySpec`, `AbilityTask`, `AbilityEvent` | UGameplayAbility… | Planlanacak |
| `Cooldown` | Cooldown GE | Planlanacak (HasDuration effect + tag) |
| `AbilitySystemComponent` | ASC | En son; alt sistemleri birleştirir, kendisi mantık taşımaz |

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
