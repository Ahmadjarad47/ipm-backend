# خطة ربط الفرونت (`ipm-web`) بالباك اند (`ipm-backend`)

> الحالة: **منفَّذ (P0–P6)**. الوضع التجريبي محذوف على الطرفين: الفرونت بلا mock (انحذف `src/app/mock`)، والباك اند `Seed__Demo=false` بـ compose (قاعدة فاضية ما عدا مدير النظام). بقيت فجوات صغيرة بالقائمة §9.

## 1. الهدف والمبادئ
ربط الفرونت بالـ API الحقيقي **بدون ما يتغيّر شكل الشاشات**، وبطريقة تنحفظ مع الوقت (ما نرجع نخرّب كل ما يتغيّر endpoint).

1. **الباك اند هو مصدر العقد.** موثّق بـ OpenAPI (`/openapi/v1.json`) ومغطّى باختبارات تكامل. الفرونت بيتكيّف معه، مش العكس، إلا إذا الشي ناقص فعلاً بالباك اند (انظر §5).
2. **طبقة عزل (Anti-Corruption Layer) بالفرونت:** الـ `domain/*.api.ts` هي الحد الوحيد اللي بيعرف مسارات الباك اند وأشكاله؛ الشاشات والـ stores بتضل على نماذجها (`domain/*.models.ts`). أي اختلاف بالشكل (أسماء، تعداد، ترقيم، إصدار) بيتحوّل بمحوّلات (mappers) جوّا الـ API، مش بالشاشات.
3. **الأمان:** رمز الوصول بالذاكرة فقط (مش `sessionStorage`)، ورمز التجديد HttpOnly cookie (الباك اند جاهز) فالـ refresh للصفحة بيرجّع الجلسة بصمت. نفس الأصل (same-origin) عبر proxy بالتطوير وreverse proxy بالإنتاج، فما في CORS مفتوح.
4. **شرائح عمودية:** كل مرحلة بتخلّي شاشات كاملة تشتغل على الحقيقي (من الواجهة للقاعدة) وبتنتهي باختبار بالمتصفح، مش «كل الـ API ثم كل الشاشات».
5. **منع الانجراف:** اختبار عقد بالفرونت (`api-contract.spec.ts`) بيفحص المسارات والأفعال وترويسة `If-Match` لكل خدمة؛ وتوليد الأنواع من OpenAPI مؤجَّل لأن ردود الـ API غير موصوفة بالـ OpenAPI (§9).
6. **الـ mock انحذف** (بقرار التنفيذ): نماذج الواجهة صارت بنفس قيم الـ API (kebab-case) فأي mock لازم يتحدّث معها. البيانات التجريبية بتجي من `Seed__Demo=true` بالباك اند (للتطوير والعرض)، واختبارات القواعد اللي كانت بالـ mock (224) صارت اختبارات الباك اند.

## 2. الفروقات الأساسية بين الطرفين (جرد)
### 2.1 النقل والاتفاقيات (تتصلّح مرة وحدة بالطبقة العامة)
| الموضوع | الفرونت (mock) | الباك اند | الحل |
|---|---|---|---|
| الأخطاء | `{ message, details{field:msg}, code: session_expired… }` | problem+json: `detail` و`errors{field:[msg]}` و`code` (`Auth.Forbidden`…) و`traceId` | محوّل بـ `errorInterceptor`: `detail→message`، `errors→details` (أول رسالة)، `code` بالرسالة للمنطق (جلسة منتهية/حساب معطّل) |
| التزامن | `version` بجسم PATCH | `If-Match` (ETag = `version`) وPUT، ولا يوجد `version` بالجسم | `BaseApiService.update/remove` بيحوّل `version` لترويسة `If-Match` ويستعمل PUT |
| الجلب الكامل | `GET /x/all` | قوائم مرقّمة (حتى 200) | `all()` بتجلب كل الصفحات (`fetchAll`) |
| الترقيم والفرز | `page,pageSize,search,sort=f:dir,filter.f=` | `page,pageSize` وفلاتر مسمّاة لكل مورد (`projectId`,`statusId`…) | `toQuery()` لكل API (افتراضياً `page/pageSize/search`) |
| التعداد | PascalCase (`NotStarted`، `High`) | kebab (`not-started`، `high`) | محوّلات تعداد مركزية بـ `core/api/enums.ts` |
| التواريخ | ISO | ISO (`DateOnly` كنص `yyyy-MM-dd`، `DateTimeOffset`) | بدون تحويل |
| الصلاحيات | جلسة بـ `systemPermissions/projectPermissions/denied` | `GET /auth/me` بنفس الشكل | تقريباً جاهز |

### 2.2 المصادقة والجلسة
| | الفرونت | الباك اند | الحل |
|---|---|---|---|
| الدخول | رد = `AuthSession` كامل | `{ accessToken, expiresAt, mustChangePassword }` + كوكي `ipm_refresh` | `login()` = `POST /auth/login` ثم `GET /auth/me` ودمج الاثنين |
| تجديد الجلسة | لا شي (sessionStorage) | `POST /auth/refresh` بالكوكي | تهيئة التطبيق (`provideAppInitializer`) بتجرب `refresh` عند البدء؛ وinterceptor بيعيد الطلب مرة بعد 401 ثم يطلّع المستخدم |
| الخروج | `POST /auth/logout` | نفسه | بدون تغيير |
| تغيير كلمة المرور | غير موجود | `POST /auth/password` + `mustChangePassword` | شاشة جديدة صغيرة (مطلوبة لمدير النظام الأول بقاعدة بدون demo) |

### 2.3 المسارات حسب المورد (مختصر؛ التفصيل بكل مرحلة)
| الفرونت | الباك اند | ملاحظة |
|---|---|---|
| `GET users/all`، `GET users` | `GET users`، `GET users/directory` (خفيف) | lookup بـ `directory` |
| `PATCH users/:id` | `PUT users/:id`، `PUT users/:id/roles`، `POST users/:id/(de)activate` | تقسيم الأوامر |
| `roles` CRUD | `roles` CRUD + `GET permissions` | قريب |
| `work-item-types`, `statuses`, `workflows`, `relation-types` | نفسها (`PUT` للتعديل) + `.../history` و`.../impact` | قريب |
| `hierarchy-rules` (GET/PUT أزواج) | `GET hierarchy-rules`، `PUT work-item-types/:id/children` | PUT ينقسم لكل نوع أب |
| `projects` + `transition/archive/unarchive/members` | `PUT projects/:id`، `POST …/transition|archive|unarchive`، `PUT …/members|work-item-types|workflow-overrides|ref-pattern` | POST⇄PUT |
| `projects/:id/repositories` وأفعال Git | `projects/:id/repositories`, `git-sync`, `rotate-secret`, `disconnect` | قريب |
| `work-items` + `transition/close/reopen/parent/type-change/bulk/board` | نفسها (`PUT parent`) | قريب |
| `relations/by-item/:id`، `by-project/:id` | `GET work-items/:id/relations`، `GET projects/:id/relations` | تغيير مسار |
| `comments/by-subject`، `PATCH comments/:id` | `GET comments?subjectType&subjectId`، `PUT comments/:id` | تغيير |
| `transitions/by-subject` | `GET work-items/:id/status-history`، `projects/:id/status-history` | تغيير |
| `approvals`، `approval-templates`، `delegations` | نفسها + `remind/resubmit/bulk-decide/match` | قريب |
| `notifications/all` | `GET notifications` (+ `unread-count`، `PATCH :id/read`، `read-all`) | تغيير |
| (جديد، الجزء 11) ربط تلغرام من الملف الشخصي | `GET/POST/DELETE telegram/link` (حالة، بدء ربط ← `{ deepLink, expiresAt }`، فك). و`POST telegram/webhook` لتلغرام نفسه (بدون تسجيل دخول، ترويسة سرّية) | جديد؛ الميزة معطّلة افتراضياً (`enabled=false`) |
| `audit` + `audit/verify` + `audit/export` (JSON) | نفسها؛ التصدير CSV | تصدير = تنزيل ملف |
| `dashboard/stats`، `search` | نفسها (`work-item`/kebab) | تعداد |
| `settings` GET/PATCH(version) | `GET/PATCH settings` (`If-Match`) | `version`→ترويسة |
| `attachments/*` | **غير موجود** (الجزء 10 خارج الإصدار الأول) | إخفاء الواجهة (feature flag) |
| `git/simulate` (محاكي) | غير موجود (للـ mock فقط) | إخفاء بدون mock |

## 3. المراحل
كل مرحلة: **نطاق ← تعديل الفرونت (API + models + شاشات) ← تعديل الباك اند إن لزم ← اختبار آلي ← تحقق يدوي بالمتصفح ← commit**. معيار الإنهاء: الشاشات المذكورة تشتغل على القاعدة الحقيقية وتبقى بعد refresh.

| # | المرحلة | الشاشات / الأجزاء | معيار الإنهاء |
|---|---|---|---|
| P0 | **الأساس** | بيئة (proxy + بيئتين)، محوّل الأخطاء، `BaseApiService` (If-Match/PUT/fetchAll)، المصادقة (login+me، refresh بالتهيئة، 401 مرة وحدة، تغيير كلمة المرور الإجباري)، تعداد مركزي | دخول admin، refresh ما بيطلّع، خروج، 401 بيجدّد |
| P1 | **الهوية والإعدادات** | المستخدمون والأدوار والصلاحيات، أنواع العمل والحالات وسير العمل وأنواع الروابط وقواعد التسلسل، الإعدادات العامة | إنشاء مستخدم ودور من الواجهة، تعديل نوع وسير عمل وإعدادات |
| P2 | **المشاريع** | قائمة وتفاصيل وإنشاء وتعديل وانتقال وأرشفة وأعضاء وقوالب وأنواع وسير مخصص | دورة مشروع كاملة |
| P3 | **عناصر العمل** | قائمة ولوحة وتفاصيل وإنشاء وتعديل وانتقال وإغلاق وإعادة فتح وأب ونوع وجماعي، الروابط، التعليقات، سجل الحالات | دورة عنصر كاملة بما فيها الحجب |
| P4 | **الموافقات** | القوالب والطلبات والقرارات والتفويض والمعالجة الجماعية والتعديل الحساس | طلب بمرحلتين من الواجهة |
| P5 | **النشاط والإشعارات والتدقيق والبحث واللوحة** | الإشعارات (شارة)، النشاط، التدقيق/التحقق/التصدير، البحث الشامل، اللوحة والتقارير والخط الزمني، Git (مستودعات، أحداث، تسليمات) | كل الشاشات الباقية |
| P6 | **التقسية والتسليم** | حذف الـ mock من بناء الإنتاج (يبقى `-c mock`)، توليد الأنواع من OpenAPI + فحص CI، reverse proxy (nginx) وحزمة docker للفرونت، Content-Security-Policy، سكربت e2e smoke، إخفاء المرفقات | بناء إنتاج، compose كامل (قاعدة + API + web) بأمر واحد |

## 4. اختبار الربط
- **الباك اند:** اختبارات التكامل الحالية (العقد) تبقى بوابة؛ أي endpoint يُضاف بسبب الربط بيجي باختباره.
- **الفرونت:** اختبارات الوحدات للمحوّلات (أخطاء، تعداد، `If-Match`) + تحقق يدوي بالمتصفح بنهاية كل مرحلة.
- **e2e smoke (P6):** Playwright على compose كامل: دخول، إنشاء مشروع وعنصر، انتقال، موافقة، إشعار.

## 5. فجوات الباك اند المتوقعة (تُقرّر عند الوصول لها)
معيار القرار: إذا الشاشة تحتاج بيانات/سلوك مش موجود → نضيفه بالباك اند (بمواصفة صغيرة + اختبار)؛ إذا الفرق بالمسار أو الشكل فقط → الفرونت بيتكيّف.
- مرشّحة: ترتيب/فرز بحقول إضافية بالقوائم، فلاتر بحث نصي بالمستخدمين، `PUT hierarchy-rules` مجمّع (بدل كل نوع أب)، أي حقل عرض ناقص بالردود (أسماء/مجاميع).
- **خارج النطاق عن قصد:** المرفقات (الجزء 10) ومحاكي Git.

## 6. المخاطر
| الخطر | التخفيف |
|---|---|
| حجم التعديل بالفرونت (نماذج بتفترض شكل الـ mock) | شرائح عمودية + محوّلات + اختبارات للمحوّلات |
| كوكي `Secure; SameSite=Strict` على `http` | `localhost` مسموح بالمتصفحات؛ وبالإنتاج HTTPS خلف reverse proxy بنفس النطاق |
| انجراف العقد مستقبلاً | أنواع مولّدة + فحص CI |
| بيانات فاضية تخفي مشاكل | نصنع بيانات عبر الـ API نفسه بكل مرحلة (سكربت seed اختياري `scripts/seed-demo.ps1` عبر API، مش عبر قاعدة البيانات) |

## 7. ما تم فعلاً
- **الفرونت (ipm-web):** نقل (problem+json، If-Match/PUT، ترقيم)، جلسة (توكن بالذاكرة + كوكي تجديد + إعادة محاولة 401 مرة + شاشة تغيير كلمة المرور الإجباري)، محوّلات لكل مورد (مستخدمون/أدوار، حالات/سير عمل، عناصر/أنواع/علاقات، مشاريع مع المستودعات، تعليقات/إشعارات/تدقيق/Git، موافقات، إعدادات)، قيم enums مطابقة للـ API، تصدير التدقيق CSV من الخادم، اختبار عقد، Dockerfile وnginx (نفس الأصل).
- **الباك اند:** فلاتر متعددة القيم لعناصر العمل (`statusId=a,b`…)، `repositories` مع المشروع لمن عنده `git.view`، فلتر `outcome` و`event` لتسليمات Git، `createdAt` للموافقة، حد تجديد الجلسة 120/دقيقة (`RateLimiting:RefreshPerMinute`) منفصل عن الدخول، خدمة `web` بـ compose (منفذ 8000).
- **التحقق:** الشاشات الرئيسية والعمليات (إنشاء وتعديل عنصر، انتقال، تعليق، إنشاء وتعديل مستخدم، إعدادات، إنشاء مشروع) اتجرّبت بالمتصفح على قاعدة تجريبية معزولة ثم على المكدّس الفعلي.

## 8. فجوات معروفة (قائمة الأعمال)
| # | الفجوة | الحالة بالواجهة |
|---|---|---|
| 1 | **المرفقات** (الجزء 10 خارج الإصدار الأول) | مخفية (`environment.features.attachments=false`) |
| 2 | **إعادة تعيين معتمد** (FR-APR-11) غير موجودة بالباك اند | مخفية (`features.approverReassign=false`) |
| 3 | **تعليقات على طلب موافقة** (الباك اند للمشاريع والعناصر فقط) | القسم محذوف من شاشة الطلب |
| 4 | **محاكي Git** (كان للـ mock فقط) | مخفي |
| 5 | **OpenAPI بلا أنواع الردود** (`Results.Ok` بدون `Produces<T>`) فتوليد الأنواع تلقائياً غير ممكن | اختبار العقد اليدوي بدلها |
| 6 | قوائم المستخدمين والأدوار بلا `version` | الواجهة بتقرأ النسخة الحالية وقت التعديل (بدون كشف تعارض بين فتح الشاشة والحفظ) |
| 7 | اختبار e2e آلي (Playwright) | غير مضاف؛ التحقق كان يدوياً بالمتصفح |
| 8 | الكوكي `Secure`: على غير `localhost` لازم HTTPS خلف reverse proxy | توثيق |
| 9 | شاشات اتجرّبت بالقراءة فقط: قوالب الموافقات، أنواع وسير العمل، تفويض الموافقات، علاقات العناصر، دورة الموافقة الكاملة | بتحتاج مرور يدوي أو e2e |

## 9. التشغيل
```powershell
docker compose up -d --build        # قاعدة + redis + seq + API (8080) + web (8000)
cd ..\ipm-web; npm install; npm start   # بديل للتطوير بتحديث فوري: http://localhost:4200 (proxy إلى 8080)
```
بيانات تجريبية (للتطوير والعرض): ضبط `Seed__Demo: "true"` بـ `docker-compose.yml` ثم إعادة تشغيل الـ API.
