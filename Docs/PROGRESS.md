# İlerleme Takibi

> Her AI/geliştirici: işe başlamadan önce oku, başlarken "Yapılıyor"a ekle, bitirince "Yapıldı"ya taşı.
> Format: `- [x] Açıklama — [Yapan, YYYY-MM-DD]`

## Yapıldı
- [x] Proje iskeleti: `Assets/`, `Core/`, `Resource/`, `Docs/` klasörleri — [Claude, 2026-11-16]
- [x] Kural/mimari/ilerleme dokümanları (`RULES.md`, `ARCHITECTURE.md`, `PROGRESS.md`, `AGENTS.md`, `CLAUDE.md`) — [Claude, 2026-11-16]
- [x] Tag sistemi planı onaylandı (`Docs/Systems/Tag.md`): kaynak `Tags.tres`, Autoload ile yükleme, ağ kapsam dışı — [Claude, 2026-11-16]
- [x] Godot 4.7.2 C# projesi: `project.godot`, `AbilitySystem.csproj` (`.cs.uid` dosyaları Godot tarafından üretildi) — [Claude, 2026-11-16]
- [x] Tag sistemi uygulandı (`Docs/Systems/Tag.md`): `Tag`, `TagRegistry`, `TagList`, `TagLoader` (Autoload), `TagContainer`, `TagCountContainer`, `Resource/Tags/Tags.tres` — [Claude, 2026-11-16]
- [x] `TagContainer` Resource'a çevrildi; `InitialTags` editörde `Tags.tres`'ten beslenen açılır menüyle seçiliyor — [Claude, 2026-11-16]

- [x] `TagContainer` depolama yapısı ölçülüp karara bağlandı: `List<Tag>` kalıyor — [Claude, 2026-11-17]

## Yapılıyor
- (boş)

## Yapılacak
- [ ] `Attribute` / `AttributeSet`
- [ ] `Modifier`
- [ ] `EffectData` (Resource) / `Effect` / `EffectSpec`
- [ ] `AbilityData` (Resource) / `Ability` / `AbilitySpec`
- [ ] `Cooldown`
- [ ] `AbilityTask`
- [ ] `AbilitySystemComponent` (en son, alt sistemleri birleştirir)
- [ ] Örnek sahne ve örnek ability (Fireball vb.)

## Sorunlar / Notlar
- Henüz ana sahne (`run/main_scene`) yok; örnek sahne eklenince `project.godot`'a girilmeli.
- `.sln` oluşturulmadı; gerekirse Godot editöründen (Project → Tools → C# → Create C# solution) üretilebilir.
- `Tags.tres`'teki tag'ler (`Ability.Fire.Ball`, `State.Dead`, `State.Stunned`) örnektir; gerçek liste sistemler yazıldıkça genişletilir.
