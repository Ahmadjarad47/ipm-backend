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
- حسابات التطوير: `admin` / `Admin12345`، وباقي المستخدمين (`khaled`، `noura`، `fahad`، `reem`، `abdullah`) كلمة سرهم `Demo12345`. المشاريع التجريبية: `ERP`، `SSP`، `BI`، `MAIL` (مؤرشف)

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
| 01 الهوية والصلاحيات | مُسلَّم | [01-identity](docs/modules/01-identity.md) |
| 02 إعدادات الأنواع | مُسلَّم | [02-work-item-types](docs/modules/02-work-item-types.md) |
| 03 سير العمل | مُسلَّم | [03-workflows](docs/modules/03-workflows.md) |
| 04 المشاريع | مُسلَّم | [04-projects](docs/modules/04-projects.md) |
| 05 عناصر العمل | مُسلَّم | [05-work-items](docs/modules/05-work-items.md) |
| 05b العلاقات والتعليقات | مُسلَّم | [05b-relations-comments](docs/modules/05b-relations-comments.md) |
| 06 محرك الموافقات | مُسلَّم | [06-approvals](docs/modules/06-approvals.md) |
| 06b التعديل الحساس | مُسلَّم | [06b-sensitive-changes](docs/modules/06b-sensitive-changes.md) |
| 07 ربط Git | مُسلَّم | [07-git-integration](docs/modules/07-git-integration.md) |
| 08 النشاط والتدقيق والإشعارات | مسودة تنتظر الموافقة | [08-activity-audit-notifications](docs/modules/08-activity-audit-notifications.md) |
| 09 | لم يبدأ | [ROADMAP](docs/ROADMAP.md) |
