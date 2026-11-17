# İlerleme — AbilitySystem

> Başlarken "Yapılıyor"a `- [ ] iş — [Yapan, YYYY-MM-DD]` ekle, bitince "Yapıldı"ya tek satır taşı. Ayrıntı git geçmişinde ve `Systems/` dokümanlarında.

## Yapıldı
- Addon iskeleti, `plugin.cfg`, `AbilitySystemPlugin` (boş)
- Attribute sistemi (`Systems/Attribute.md`), 57 test
- Effect planı (`Systems/Effect.md`) — [Claude, 2026-11-17]
- Effect Faz 0: `Aggregator` → `AttributeSet`, `ModifierSource`, `ModifierEventData`, `ExecuteModifier`, `PostEffectExecute`, `ModifierApplied`; 40 test — [Claude, 2026-11-17]

## Yapılıyor
- (boş)

## Yapılacak (sırayla, `Systems/Effect.md` → Uygulama Sırası)
- [ ] Timers addon'u (ayrı repo, Godot-AI-Timers): `TimerManager`, `TimerHandle`
- [ ] Effect Faz 1 (Tags'te `TagContainer.BuildTagOptions` public)
- [ ] Effect Faz 2: stacking, `AttributeBased` magnitude
- [ ] Effect Faz 3: özel magnitude / execution, süregelen tag şartları, bağışıklık
- [ ] Ability, Cooldown, AbilityTask, ASC, örnek sahne (Fireball)

## Notlar
- `ModifierSource/`, `ModifierEventData/` için `.cs.uid` dosyaları henüz yok; Godot editörü açılınca üretilir, sonra commit'lenmeli.
- `Attribute.md`'nin gövdesi Faz 0 öncesini anlatıyor; bir sonraki Attribute işinde güncellenmeli.
- Ana sahne (`run/main_scene`) yok; örnek sahne eklenince proje kökündeki `project.godot`'a girilmeli.
- Resource tabanlı attribute set fikri ertelendi (`Systems/Attribute.md` → İleride).
