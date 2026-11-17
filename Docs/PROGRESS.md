# İlerleme — AbilitySystem

> Başlarken "Yapılıyor"a `- [ ] iş — [Yapan, YYYY-MM-DD]` ekle, bitince "Yapıldı"ya tek satır taşı. Ayrıntı git geçmişinde ve `Systems/` dokümanlarında.

## Yapıldı
- Addon iskeleti, `plugin.cfg`, `AbilitySystemPlugin` (boş)
- Attribute sistemi (`Systems/Attribute.md`), 57 test
- Effect planı (`Systems/Effect.md`) — [Claude, 2026-11-17]
- Effect Faz 0: `Aggregator` → `AttributeSet`, `ModifierSource`, `ModifierEventData`, `ExecuteModifier`, `PostEffectExecute`, `ModifierApplied`; 40 test — [Claude, 2026-11-17]
- Timers addon'u yazıldı (ayrı repo, `addons/Timers/`); effect'ler statik `TimerManager`'ı kullanacak — [Claude, 2026-11-17]
- Effect Faz 1: `EffectHandle`, `Modifier`, `EffectData`, `EffectSpec`, `ActiveEffect`, `ActiveEffectContainer`; Godot içinde 38 test — [Claude, 2026-11-17]
- `Magnitude` resource'u (Constant/SetByCaller/AttributeBased snapshot); süre ve periyot da Magnitude; `EffectData` grupları; 48 test — [Claude, 2026-11-17]
- `Magnitude.Snapshot` (kapalıysa her kullanımda okunur, süreli effect attribute'u takip eder), Pre/PostMultiplyAdd kaldırıldı; `Aggregator.SetMagnitude`, `UpdateModifier`, `ModifierEventKind.Updated`; 11 test — [Claude, 2026-11-17]

## Yapılıyor
- (boş)

## Yapılacak (sırayla, `Systems/Effect.md` → Uygulama Sırası)
- [ ] Editörde `EffectData`/`Modifier`/`Magnitude` inspector'ını dene (gruplar, gizlenen alanlar, menüler, `.tres` kaydı); headless testte denenemiyor
- [ ] Effect Faz 2: stacking
- [ ] Effect Faz 3: özel magnitude / execution, süregelen tag şartları, bağışıklık
- [ ] Ability, Cooldown, AbilityTask, ASC, örnek sahne (Fireball)

## Notlar
- Testler headless Godot'ta: `Godot_..._console.exe --headless --path <proje> res://<test sahnesi>`; geçici test sahnesi proje kökünde açılıp sonra silinir. Timers eklentisi kapalıysa test `TimerManager` düğümünü kendisi ekler.
- Headless Godot çalıştırması yeni script'lerin `.cs.uid` dosyalarını da üretir; commit'e dahil et.
- `Attribute.md`'nin gövdesi Faz 0 öncesini anlatıyor; bir sonraki Attribute işinde güncellenmeli.
- Ana sahne (`run/main_scene`) yok; örnek sahne eklenince proje kökündeki `project.godot`'a girilmeli.
- Resource tabanlı attribute set fikri ertelendi (`Systems/Attribute.md` → İleride).
