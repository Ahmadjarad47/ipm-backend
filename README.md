# IPM Backend

API لنظام إدارة المشاريع الداخلي (IPM)، مبني جزءاً جزءاً حسب [خط الإنتاج](docs/MODULE-PIPELINE.md) و[الخارطة](docs/ROADMAP.md).
.NET 10 · Clean Architecture · CQRS (Mediator) · EF Core + PostgreSQL 17 · Redis 7 · Serilog + Seq · Scalar.

## التشغيل
```powershell
docker compose up -d --build     # Postgres 55432، Redis 6379، Seq 5341، الـ API على 8080
```
- الصحة: http://localhost:8080/health
- التوثيق: http://localhost:8080/docs (الوصف: `/openapi/v1.json`)
- أمثلة الروابط: [docs/http](docs/http)

بدون الـ API بالحاوية: `docker compose up -d postgres redis seq` ثم `dotnet run --project src/Ipm.Api`.

## البناء والاختبار
```powershell
dotnet build Ipm.slnx            # التحذيرات أخطاء
dotnet test Ipm.slnx             # اختبارات التكامل تحتاج Docker (Testcontainers)
```

## البنية
`Domain` ← `Application` ← `Infrastructure` ← `Api` (الاعتماد للداخل فقط، اختبارات `ArchitectureTests` بتفرضه).
قواعد المجلدات: عملية بمجلد، نوع واحد بالملف، الـ endpoint بدون منطق.

## الأجزاء
| الجزء | الحالة | المواصفة |
|---|---|---|
| 00 الأساس | مُسلَّم | [00-foundation](docs/modules/00-foundation.md) |
| 01–09 | لم يبدأ | [ROADMAP](docs/ROADMAP.md) |
