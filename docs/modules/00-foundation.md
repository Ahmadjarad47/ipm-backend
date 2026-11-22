# الجزء 00: الأساس — المواصفة

> المرحلة 0 من [خط الإنتاج](../MODULE-PIPELINE.md). الحالة: **مُسلَّم**.
> البنية: skill `clean-backend-dotnet` (مرجعها التنفيذي `erp101-backend`). الـ solution: `Ipm`، .NET 10.

## 1. الهدف
بنية مشتركة جاهزة حتى كل جزء بعدها يبلّش مباشرة بجداوله وقواعده بدون ما يعيد بناء نفس الأشياء.
هالجزء ما فيه جداول عمل ولا روابط عمل، وما فيه مصادقة (بتجي بالجزء 01).

## 2. شو بينبني
| # | العنصر | التفصيل |
|---|---|---|
| A | **هيكل الـ solution** | `Ipm.Domain` · `Ipm.Application` · `Ipm.Infrastructure` · `Ipm.Api` + `tests/Ipm.Domain.UnitTests` · `Ipm.ArchitectureTests` · `Ipm.IntegrationTests`. `Directory.Build.props` (التحذيرات أخطاء)، `Directory.Packages.props` (إصدارات مركزية)، `global.json`، `.editorconfig` |
| B | **الأساس بالمجال** | `Entity` (UUIDv7 + `Version` = `xmin`)، `AuditableEntity`، `IDomainEvent`، `Result` / `Error` بأنواعها (400/401/403/404/409/412/422) |
| C | **CQRS** | Mediator (مولَّد بالكومبايلر)، `ICommand` / `IQuery` + handlers، سلسلة Logging → Validation → QueryCaching، `PagedList` بصيغة `{ items, page, pageSize, total }` |
| D | **سجل التدقيق** | جدول `public.audit_log` (`id`، `entity`، `entity_id`، `action`، `before`/`after` jsonb، `user_id`، `at`) بيعبّيه interceptor بنفس المعاملة لكل كيان `IAuditable`، مع `.ExcludeFromAudit()` للحقول السرية |
| E | **منع تضارب التعديلات** | `ETag` على قراءة السجل الواحد، و`If-Match` على التعديل والحذف. بدون الترويسة: **428**، صيغة خاطئة: **400**، نسخة قديمة: **412** |
| F | **صيغة الأخطاء بالعربي** | `application/problem+json` بعنوان عربي، `detail` عربي، `code` ثابت، `traceId`. رسائل FluentValidation بالعربي وأسماء الحقول camelCase. 500 بدون تفاصيل داخلية. تكرار القيمة الفريدة ← 409 `Conflict.Duplicate` |
| G | **التخزين** | `ApplicationDbContext` (EF Core + Npgsql، snake_case، schema لكل مجال بـ `Schemas.cs`)، interceptors (الأختام، التدقيق، أحداث المجال)، Redis (`ICacheService`)، الـ Enums نص kebab-case بالـ JSON وقاعدة البيانات وكويري سترنغ |
| H | **اكتشاف الروابط** | `IEndpoint` مكتشَف تلقائياً تحت `/api/v1`، كل شي يحتاج تسجيل دخول افتراضياً (`AllowAnonymous` صريح) |
| I | **التوثيق** | Scalar على `/docs` فوق `/openapi/v1.json`، مفعّل بالإعداد `OpenApi:Enabled`، كل جزء بمجموعة (tag) عربية لحاله |
| J | **التشغيل** | `Dockerfile` وـ`docker-compose.yml` (PostgreSQL 17 على المنفذ 55432، Redis 7، Seq، الـ API على 8080)، الـ migrations بتنطبق عند الإقلاع بالتطوير، `/health` لـ Postgres وRedis |
| K | **الاختبارات** | اختبارات اتجاه الطبقات، واختبارات بنية المجلدات (عملية بمجلد، نوع واحد بالملف)، وتكامل بـ `WebApplicationFactory` + Testcontainers (Postgres وRedis) مع قاعدة نظيفة |
| L | **أول migration** | `Initial`: فيه `audit_log` بس |
| M | **ملف الروابط** | `docs/http/00-foundation.http` (الصحة والتوثيق) كنموذج للأجزاء الجاية |

> ما بيدخل بهالجزء: `Serilog` + `Seq` للسجلات (مثل الـ ERP)، لكن الـ rate limiting و`ForwardedHeaders` بيتأجلوا للجزء 01 (الدخول).

## 3. الروابط
| الرابط | الرد |
|---|---|
| `GET /health` | 200 `Healthy` إذا Postgres وRedis شغالين، وإلا 503 |
| `GET /openapi/v1.json` | وصف الروابط |
| `GET /docs` | صفحة التوثيق |

## 4. الإعدادات
| المفتاح | التطوير | الإنتاج |
|---|---|---|
| `ConnectionStrings:Database` / `:Cache` | قيم docker-compose | متغيرات بيئة |
| `Database:MigrateOnStartup` | `true` | `false` |
| `OpenApi:Enabled` | `true` | `false` |

## 5. سيناريوهات القبول
1. `docker compose up -d` على جهاز نظيف بيشغّل Postgres وRedis وSeq والـ API، و`GET /health` بيرجّع 200.
2. وقّف Redis ← `/health` بيرجّع 503.
3. `/docs` بتفتح وبتعرض الروابط.
4. رابط مش موجود ← 404 problem+json بعنوان عربي.
5. خطأ غير متوقع ← 500 بعنوان ورسالة عربية و`traceId`، بدون تفاصيل داخلية.
6. أمر بيفشل بالتحقق ← 400 بعنوان عربي وخطأ عربي لكل حقل (camelCase).
7. حفظ كيان تجريبي ← سطر بـ `audit_log` فيه القيم قبل وبعد والمستخدم والوقت.
8. تعديلين على نفس الكيان بنفس الـ ETag ← التاني بياخد 412؛ بدون `If-Match` ← 428.
9. `dotnet build` بدون تحذيرات، `dotnet test` ناجح بالكامل، واختبارات التكامل شغالة على Postgres حقيقي.
10. اختبار البنية بيفشّل إذا انكسر اتجاه الطبقات أو قاعدة المجلدات.

## 6. قرارات أخدتها (صحّحني إذا لازم)
- **الكيان التجريبي (probe)** للسيناريوهين 7 و8 موجود بمشروع الاختبارات بس (جدول `test.probe_items`)، ما بينزل بالإنتاج. أول تجربة حقيقية إلهم بالجزء 01.
- **نفس مكتبات الـ ERP وإصداراتها** (Mediator، FluentValidation، EF Core 10، Npgsql، Scalar، Testcontainers) لأنها مجرّبة، وبتتحدّث بمكان واحد (`Directory.Packages.props`).
- **428 على التعديل بدون `If-Match`**، حتى ما حدا يكتب فوق تعديل غيره بالغلط (متل ما الواجهة بتبعت `version` دايماً).
- **التدقيق** بيسجّل كل الكيانات `IAuditable`؛ حقول كلمات السر بتنستثنى من الجزء 01.
- **الواجهة الحالية** بتبعت `version` بجسم الطلب؛ الخادم بيستعمل `If-Match`. التعديل على الواجهة (لما ننتقل للـ API الحقيقي) مش من شغلنا، وبنوثّقه بملف الروابط.
- **CI** مش ضمن هالجزء.
- أول commit بيكون بمرحلة التسليم لهالجزء.

## 7. مش ضمن الجزء
الدخول والصلاحيات (الجزء 01)، أي جدول عمل، الملفات وSignalR، النشر على سيرفر.
