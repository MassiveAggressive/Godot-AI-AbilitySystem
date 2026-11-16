Godot C# ile bir AbilitySystem yaratmak istiyorum. Bu ability sistemi unreal engine GAS sistemine benzeyecek.

İsminde gameplay ön eki olmayacak. 

Sistem modüler olacak, esnek olacak, kodlar okunaklı olacak, aşırı soyutlama olmayacak, aşırı bağımlılıklar olmayacak, bütün işi tek bir sınıf veya script sırtlanmayacak, her sistem kendi sınıfında, kendi scriptinde yazılacak.

Dosya yapısı: Assets, Core, Resource gibi ana klasörler olacak. her sınıf ve her sahne kendi klasöründe olacak, o klasör de Core klasöründe olacak. Mesela A isminde bir sınıfımız var, A sınıfı A klasöründe A klasörü de Core klasöründe olacak. Eğer A sınıfından bir sahne yaratıldıysa o da A klasöründe olacak. Assetler Assets klasöründe olacak. Resource lar Resource klasöründe ama resource un sınıfı yada scripti yine Core klasöründeki kendi klasöründe.

Sahne yaratma kuralı: 
- **Doğrudan Özel Sınıftan Yaratım (`metadata/_custom_type_script`)**: Sahnelere önce standart bir düğüm (`Node2D`, `Node`, `Control`) ekleyip sonra script bağlama yaklaşımı yasaktır. Özel C# sınıflarından türetilen tüm düğümler `.tscn` dosyasında `[ext_resource type="Script" uid="uid://..." ...]` ve ilgili `.cs.uid` kimliğini taşıyan `metadata/_custom_type_script = "uid://..."` ile doğrudan kendi sınıfından yaratılmış düğüm olarak tanımlanmalıdır:
  ```ini
  [ext_resource type="Script" uid="uid://bbf8lyuj8omp1" path="res://Core/Projectile/Projectile.cs" id="1_6p3um"]

  [node name="Projectile" type="Node2D"]
  script = ExtResource("1_6p3um")
  metadata/_custom_type_script = "uid://bbf8lyuj8omp1"
  ```

Birden fazla yapay zeka bu projeye müdahal ediyor olacak. Beraber çalışabileceğiniz md dosyaları yada gerekli diğer dosyaları oluştur. Projede neler yapıldı, neler yapılıyor, neler yapılacak gibi şeylerin yazılı olduğu dosyalar olsun. Diğer yapay zekaların da bunları okuyup bu kurallara uyması gerektiğini hatırlatan yazı ve dosyaları da yaz.

Performans önemli bir detay.

Mümkün olduğunda C# kullanılsın. Godot değişkenleri, dizileri, delegate leri gereksizse kullanılmasın.

Bunları dikkate alarak proje dosya ve klasörlerini oluştur, kod yazma.