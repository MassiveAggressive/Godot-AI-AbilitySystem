# İlerleme — AbilitySystem

> Başlarken "Yapılıyor"a `- [ ] iş — [Yapan, YYYY-MM-DD]` ekle, bitince "Yapıldı"ya tek satır taşı. Ayrıntı git geçmişinde ve `Systems/` dokümanlarında.

## Yapıldı
- Addon iskeleti, `plugin.cfg`, `AbilitySystemPlugin` (boş)
- Attribute sistemi (`Systems/Attribute.md`), 57 test
- Effect planı (`Systems/Effect.md`) — [Claude, 2026-11-17]

## Yapılıyor
- (boş)

## Yapılacak (sırayla, `Systems/Effect.md` → Uygulama Sırası)
- [ ] Effect Faz 0: `Aggregator`'ı `AttributeValue`'dan `AttributeSet`'e taşı; `ModifierSource` + `ModifierEventData`
- [ ] Timers addon'u (ayrı repo, Godot-AI-Timers): `TimerManager`, `TimerHandle`
- [ ] Effect Faz 1 (Tags'te `TagContainer.BuildTagOptions` public)
- [ ] Effect Faz 2: stacking, `AttributeBased` magnitude
- [ ] Effect Faz 3: özel magnitude / execution, süregelen tag şartları, bağışıklık
- [ ] Ability, Cooldown, AbilityTask, ASC, örnek sahne (Fireball)

## Notlar
- Ana sahne (`run/main_scene`) yok; örnek sahne eklenince proje kökündeki `project.godot`'a girilmeli.
- Resource tabanlı attribute set fikri ertelendi (`Systems/Attribute.md` → İleride).
