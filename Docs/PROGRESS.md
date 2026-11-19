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

- Effect Faz 2: stacking (`ByTarget`/`BySource`, limit, süre/periyot yenileme, bitiş politikası); 18 test — [Claude, 2026-11-17]
- Effect Faz 3 planı (`Systems/Effect.md` → Faz 3), kullanıcı onaylı — [Claude, 2026-11-17]
- Effect Faz 3: Custom magnitude (`MagnitudeCalculation`), execution (`ExecutionCalculation`, `ExecutionOutput`), `CalculationContext`, inhibition, `RemovalTags`, bağışıklık; 60 test — [Claude, 2026-11-17]
- Editör denemesi (kullanıcı): gruplar, gizlenen alanlar, tag ve attribute menüleri, `Calculation` ve `Executions` menüleri çalışıyor; `[Tool]` olmayan hesaplama alt sınıfları için `Resource` tipli export ile düzeltildi — [Claude, 2026-11-17]
- Örnek sınıflar proje kökünde `Resource/` altında: `CombatSet`, `ArmorDamage`, `MissingHealthHeal` (repoya girmez) — [Claude, 2026-11-17]
- Ability planı taslağı (`Systems/Ability.md`, Faz 1–3, açık kararlar K1–K6) — [Claude, 2026-11-18]

- Ability Faz 1: `Ability`, `AbilityHandle`, `AbilityContainer`, `ActivationFailure`, `AbilityTask`, `WaitDelayTask`, `AbilitySystemComponent`; Effect/Attribute ekleri; 71 test — [Claude, 2026-11-18]

- Ability Faz 2: `AbilityEventData`, `SendEvent`, tetikleyiciler (olay, tag geldi, tag varken), pasif, giriş (`PressInput`/`ReleaseInput`, ASC `ProcessInput`), `WaitEventTask`, `WaitTagTask`, `WaitInputReleaseTask`; Faz 1 ile birlikte 108 test — [Claude, 2026-11-18]

- Temizlik: `Attribute.md` gövdesi güncel koda göre düzeltildi (aggregator set'te, `ModifierSource`, `ExecuteModifier`, `UpdateModifier`, `ModifierApplied`, `CreateSet`); eski notlar silindi — [Claude, 2026-11-18]
- Ability Faz 3: Fireball örnek sahnesi (`Resource/Scenes/FireballDemo/`, `Resource/Abilities/Fireball.*`, `Resource/Effects/`); headless uçtan uca denendi (repoya girmez) — [Claude, 2026-11-18]

- Seviye ve eğri (`Systems/Level.md`): `Magnitude.LevelValues`/`LevelCurve`, `EffectSpec.Level`, `ActiveEffect.Level`, `CalculationContext.Level`, `Ability.Level` float, `AbilityContainer.SetLevel`; 26 yeni test, toplam 134 — [Claude, 2026-11-18]

- Cue sistemi (`Systems/Cue.md`): `CueParameters`, `ICueNotify`, `CueEntry`, `CueLibrary` (proje ayarı `ability_system/cue_library_path`), `CueContainer`, `EffectData.Cues`, `AbilitySystemComponent.Cues`; 58 test — [Claude, 2026-11-18]
- Cue editör denemesi (kullanıcı) tamam; örnek cue sahneleri ve Faz 3 testlerinin yeniden çalıştırılması kullanıcı kararıyla yapılmadı — [Claude, 2026-11-18]

- Hedefleme Faz 1–2 (`Systems/Targeting.md`, K1–K7 kullanıcı onaylı): `IAbilitySystem`, `TargetHit`, `TargetData`, `TargetQuery2D`, `Ability.ApplyEffectToTargets`/`ApplyEffectToTarget`, `AbilityEventData.Targets`, `TryActivate(handle, in AbilityEventData)`; 70 test headless geçti (Sonnet) — [Claude, 2026-11-18]
- Hedefleme Faz 3 planı taslağı (`Systems/Targeting.md`, K8–K15, onay bekliyor) — [Claude, 2026-11-19]
- Hedefleme Faz 3 (K8–K14 öneriler gibi, örnek sahne yok): `WaitTargetDataTask`, `TargetConfirmation`, `TargetReticle2D`, `Ability.WaitTargetData`, `AbilityTask.OnConfirm`/`OnCancel`, `AbilityContainer.InputConfirm`/`InputCancel`, ASC `ConfirmAction`/`CancelAction`; 111 test headless geçti (Sonnet) — [Claude, 2026-11-19]
- Plan taslakları: takım ilişkisi (`Targeting.md` Faz 4, K16–K19), ability cue'ları + vuruş noktası (`Cue.md` Faz 2, C1–C6), tag seçme ağacı (Tags reposu `Docs/Systems/TagPicker.md`, T1–T5) — [Claude, 2026-11-19]

- Plan onayı (kullanıcı): K16–K19, C1–C6, T1–T5 öneriler gibi — [Claude, 2026-11-19]
- Hedefleme Faz 4 (takım ilişkisi): `TeamAttitude`, `TeamAttitudeMask`, `IAbilitySystem.Team`/`GetAttitude`, ASC `TeamName`/`Team`, `TargetQuery2D.Attitudes`; derleme temiz, testler kullanıcıda — [Claude, 2026-11-19]

## Yapılıyor

## Yapılacak (sırayla)
- [ ] Cue Faz 2 (ability cue'ları, vuruş noktası)
- [ ] Tag seçme ağacı (Tags reposu), sonra AbilitySystem alanlarının `TagNameHint`'e geçmesi

## Notlar
- Testler headless Godot'ta: `Godot_..._console.exe --headless --path <proje> res://<test sahnesi>`; geçici test sahnesi proje kökünde açılıp sonra silinir. Timers eklentisi kapalıysa test `TimerManager` düğümünü kendisi ekler.
- Sahne testleri ana sahne olarak da çalıştırılmalı (`--headless --path <proje> res://<sahne>.tscn --quit-after N`): ana sahne autoload'lar ağaca girmeden instantiate edilir; başka sahneden yüklemek bu sırayı yakalamaz.
- Yeni script'lerin `.cs.uid` dosyaları test sahnesi çalıştırmasıyla değil, `--headless --path <proje> --import` ile üretilir; commit'e dahil et.
- `run/main_scene` şu an kullanıcının `Test/AbilityDemo` sahnesi; Fireball örneği (`Resource/Scenes/FireballDemo/FireballDemo.tscn`) F6 ile açılır, ana sahne yapılmadı.
- Resource tabanlı attribute set fikri ertelendi (`Systems/Attribute.md` → İleride).
- Editörde `[Tool]` olmayan `[GlobalClass]` alt sınıf yalnızca yer tutucu `Resource` olur; alt sınıf beklenen export alanları `Resource` tipinde tutulup çalışma anında cast edilir (`Magnitude.Calculation`, `EffectData.Executions`).
- RULES "döngüsel bağımlılık yasak" ile Faz 3'teki katman içi tip döngüsü (`EffectData` → hesaplama → `CalculationContext` → `EffectSpec`) kullanıcı kararıyla kabul edildi (`Effect.md` → Bağımlılıklar).
