# الجزء 01: الهوية والصلاحيات — المواصفة

> المرحلة 0 من [خط الإنتاج](../MODULE-PIPELINE.md). الحالة: **مُسلَّم**.
> يعتمد على: [الجزء 00](00-foundation.md) (مُسلَّم).
> المرجع (للفهم فقط): `ipm-web/src/app/core/auth/*` (المفاتيح والجلسة)، `domain/users/user.models.ts`،
> `mock/server/handlers/auth-users.handlers.ts` و`config.handlers.ts` (`validateRole`، `guardRole`)، `mock/db/seed.ts`،
> والـ SRS §2.2 و§2.3 و FR-AUTH-01…06.

## 1. الهدف
المستخدمون والأدوار والصلاحيات بالخادم، مع تسجيل دخول حقيقي، والخادم هو اللي بيفرض الصلاحية (403) مش الواجهة.
بعد هالجزء كل رابط بيعلن مفتاح صلاحية واحد، وبيستبدل «المصادقة المؤقتة» اللي بالجزء 00 (`PlaceholderAuthentication`).
كل الأجزاء الجاية بتعتمد عليه: مين المستخدم، وشو مسموحله.
**المستخدم (`User`) هو نفسه `IdentityUser<Guid>`** (قرار الخارطة).

## 2. الجداول (schema `identity`)
| الجدول | الأعمدة | القيود |
|---|---|---|
| `users` | حقول Identity (`id`، `user_name`، `email`، `password_hash`، `lockout_end`، `access_failed_count`، `security_stamp`…) + `name`، `title`، `is_active`، `manager_id`، `must_change_password` + الأختام + `xmin` | `user_name` فريد: `^[a-z][a-z0-9._-]{2,30}$`؛ `email` فريد بدون حساسية حالة؛ `manager_id` FK ذاتي (`ON DELETE RESTRICT`)؛ `name` 1–100، `title` ≤100 |
| `roles` | `id`، `key`، `name`، `description`، `scope` (`system`/`project`)، `is_built_in`، **`permissions text[]`**، **`denied text[]`** + الأختام + `xmin` | `key` فريد (snake_case، ثابت)؛ `name` فريد بدون حساسية حالة؛ CHECK على `scope`؛ CHECK إنه ما في مفتاح بالمصفوفتين معاً، وكل المفاتيح من القائمة المعروفة |
| ~~`user_roles`~~ | **ما انعمل جدول**: أدوار المستخدم عمود `role_ids uuid[]` بجدول `users` (انظر «تعديلات أثناء التنفيذ») | — |
| `refresh_tokens` | `id`، `user_id`، `token_hash`، `created_at`، `expires_at`، `rotated_at`، `revoked_at` | فهرس فريد على `token_hash`؛ فهرس على `(user_id)` |
| `security_events` | `id`، `at`، `type` (`login-succeeded`، `login-failed`، `locked-out`، `permission-denied`، `password-changed`، `password-reset`، `sessions-revoked`)، `user_id` (null)، `username`، `ip`، `reason`، `permission` (null) | append-only؛ فهرس `(at)` و`(user_id, at)` |
| جداول Identity المساعدة | `user_claims`، `user_logins`، `user_tokens` | بنفس الـ schema |

- المعرّفات UUIDv7. ما في `r-admin` نصّي: الأدوار بتتعرّف بالـ `key`.
- `password_hash` و`security_stamp` و`concurrency_stamp` و`token_hash` مستثناة من سجل التدقيق (`ExcludeFromAudit`).
- **صلاحيات الدور عمود `text[]` وليست جدول** (نفس تجربة الـ ERP): تغيير الصلاحيات بيغيّر `xmin` وبيظهر بـ `audit_log`.
- **صلاحيات على مستوى المشروع:** أدوار `scope = project` (مدير مشروع، معتمد، عضو، قارئ) بتنعرّف وبتنخزّن هون، لكن **ربط المستخدم بالمشروع بدور (الأعضاء) هو الجزء 04**.
  الخدمة المركزية `IPermissionService.Can(userId, key, projectId?)` بتنبني هون بقاعدة «منع صريح يعلو السماح» (SRS §2.2)، وفرعها الخاص بالمشروع بيرجّع «ما في» لحد ما الجزء 04 يضيف الأعضاء.
- **مدير النظام:** كل مفاتيح الصلاحيات المعروفة، وبيتحدّث تلقائياً عند إضافة مفتاح جديد بالكود (الـ seeder بيعيد مزامنة صلاحيات الدور المدمج `system_admin` عند كل إقلاع).

## 3. الصلاحيات
- **42 مفتاح** ثابتة بالكود (`Permissions.cs`) بنفس أسماء الواجهة (`PERMISSION_GROUPS`)، بعشر مجموعات:
  projects(5)، tasks(5)، workflow(3)، approvals(4)، collaboration(5)، integration(3)، security(6)، reporting(3)، workItemTypes(4)، relationships(4).
  مفتاح جديد بيتضاف بالكود فقط (SRS §2.3)، والتوزيع على الأدوار ديناميكي.
- بتنقرأ من Redis (`perm:{userId}`، 10 دقايق)، وبتنمسح فوراً عند: تعديل أدوار المستخدم، تعديل صلاحيات دور، تعطيل المستخدم، تغيير كلمة سره. **مش جوّا الـ JWT**، حتى أي تعديل يسري بالطلب اللي بعده.
- المنطق: `denied` من أي دور يعلو `permissions` من أي دور؛ بدون منح = ممنوع (Default Deny).
- **المفاتيح اللي بيستعملها هالجزء:**

| الصلاحية | بتفتح |
|---|---|
| `roles.manage` | إنشاء الأدوار وتعديلها وحذفها |
| `permissions.assign` | إنشاء المستخدمين وتعديلهم وتعطيلهم وإسناد أدوارهم وإعادة تعيين كلمة السر، وقراءة قائمة المستخدمين الكاملة (بريد، مدير، أدوار) |
| (أي مستخدم مسجّل) | قراءة الأدوار والصلاحيات والدليل المختصر للمستخدمين، `/auth/me`، تغيير كلمة سره |

## 4. قواعد العمل
| # | القاعدة | الخطأ |
|---|---|---|
| R1 | الدخول باسم المستخدم وكلمة السر. رسالة وحدة لكل الأسباب (مجهول، كلمة غلط، معطّل) حتى ما تكشف وجود الحساب | 401 `Auth.InvalidCredentials` |
| R2 | القفل: 5 محاولات فاشلة بـ 15 دقيقة ← قفل 15 دقيقة. المحاولة بكلمة صح خلال القفل بترجع نفس الخطأ | 429 `Auth.LockedOut` (فيها الدقايق المتبقية) |
| R3 | المستخدم المعطّل ما بيدخل، وكل جلساته (refresh tokens) بتنلغي فوراً، وتوكن الوصول الجاري بينرفض بأول طلب (فحص `is_active` بالـ validation) | 401 |
| R4 | إعادة استعمال refresh token قديم ← كل جلسات المستخدم بتنلغى (سرقة محتملة) | 401 `Auth.RefreshInvalid` |
| R5 | سياسة كلمة السر: 8 أحرف على الأقل، فيها حرف ورقم | 400 `Validation.Failed` |
| R6 | كلمة السر الأولى (أو بعد إعادة التعيين) مؤقتة: `mustChangePassword = true` لحد ما المستخدم يغيّرها. وهو بالحالة هاي ما بيقدر يستعمل أي رابط غير `/auth/*` | 403 `Auth.PasswordChangeRequired` |
| R7 | اسم المستخدم والبريد فريدين (البريد بدون حساسية حالة) | 409 `User.UsernameTaken` / `User.EmailTaken` |
| R8 | المستخدم **ما بينحذف**، بيتعطّل بس (لأن المهام والموافقات رح تأشّر عليه) | — |
| R9 | ما بتقدر تعطّل حسابك إنت | 422 `User.CannotDeactivateSelf` |
| R10 | ما بتقدر تعدّل أدوارك إنت | 403 `User.CannotChangeOwnRoles` |
| R11 | **سقف الامتيازات:** ما بتقدر تعطّل أو تعيّن أدوار أو تعيد تعيين كلمة سر لحساب بيملك صلاحية إنت ما بتملكها | 403 `User.TargetOutranksCaller` |
| R12 | **ما بتمنح إلا اللي بتملكه:** ما بتسند دور فيه صلاحية ما بتملكها، وما بتضيف صلاحية لدور إلا إذا بتملكها (الحذف والمنع مسموحين دايماً) | 403 `Role.PermissionNotGrantable` |
| R13 | لازم يضل **مستخدم نشط واحد على الأقل بصلاحية `roles.manage` فعّالة**: ممنوع تعطيله أو سحب دوره أو تعديل دور بيخلّي العدد صفر | 422 `User.LastRoleManager` |
| R14 | المدير المباشر (`manager_id`) لازم يكون مستخدم **نشط**، مو نفسه، وسلسلة المدراء بدون حلقة دائرية | 400 (حقل `managerId`) |
| R15 | الأدوار المدمجة (`is_built_in`) بتتعدّل (الاسم والوصف والصلاحيات) بس ما بتنحذف. و`key` و`scope` و`is_built_in` ثابتة بعد الإنشاء | 422 `Role.BuiltInCannotBeDeleted` / `Role.ImmutableField` |
| R16 | الدور المسند لمستخدمين ما بينحذف (مع العدد) | 409 `Role.InUse` |
| R17 | اسم الدور فريد | 409 `Role.NameTaken` |
| R18 | مفتاح الصلاحية لازم يكون من القائمة المعروفة، وما بينمنح ويتمنع بنفس الدور | 400 (حقل `permissions` / `denied`) |
| R19 | تعيين دور لمستخدم لازم يكون `scope = system` (أدوار المشروع بتنسند بالجزء 04) | 422 `User.RoleScopeInvalid` |
| R20 | إخفاء بيانات الدليل: من ما بيملك `permissions.assign` بيشوف `{id، name، username، title، active}` فقط (بدون بريد ولا مدير ولا أدوار) | — |
| R21 | كل محاولة دخول (نجحت/فشلت/قُفل) ومنع صلاحية (403) وتغيير كلمة سر بيتسجّل بـ `security_events` (الفاشلة لاسم مجهول: مرة بالدقيقة لكل اسم، وأقصى 1000 اسم متتبَّع) | — |

## 5. الروابط (`/api/v1`، مجموعات: «الدخول»، «المستخدمون»، «الأدوار والصلاحيات»)
| الرابط | الصلاحية | الطلب ← الرد | ملاحظات |
|---|---|---|---|
| `POST /auth/login` | بدون | `{ username, password }` ← `{ accessToken, expiresAt, mustChangePassword }` + cookie | تحديد معدل: 10/دقيقة لكل IP (429) |
| `POST /auth/refresh` | cookie | ← نفس رد الدخول | rotation، R4 |
| `POST /auth/logout` | مسجّل | 204 | بيلغي الـ refresh token الحالي |
| `GET /auth/me` | مسجّل (حتى مع R6) | ← `{ user:{id،name،username،email،title}, systemPermissions[], projectPermissions{}, denied[], mustChangePassword }` | `projectPermissions` فاضية لحد الجزء 04. بنفس شكل `AuthSession` بالواجهة |
| `POST /auth/password` | مسجّل (حتى مع R6) | `{ currentPassword, newPassword }` ← 204 | بيلغي باقي الجلسات ويصفّر `mustChangePassword` |
| `GET /permissions` | مسجّل | مفاتيح مجمّعة بالمجموعات `[ {group, label, keys[]} ]` | |
| `GET /roles` | مسجّل | ترقيم + `?scope=` ← عناصر مع `userCount` | المدمجة أولاً ثم بالاسم |
| `GET /roles/{id}` | مسجّل | ← الدور + `permissions[]` + `denied[]` | ETag |
| `POST /roles` | `roles.manage` | `{ name, description, scope, permissions[], denied[], copyFromRoleId? }` ← 201 | R12، R17، R18 |
| `PUT /roles/{id}` | `roles.manage` | `{ name, description, permissions[], denied[] }` | If-Match، R12، R13، R15، R17، R18 |
| `DELETE /roles/{id}` | `roles.manage` | 204 | If-Match، R15، R16 |
| `GET /users/directory` | مسجّل | كل المستخدمين (مختصر، R20) | للاختيار بالقوائم (مسؤول، مراجع…) |
| `GET /users` | `permissions.assign` | ترقيم + `?q=` (اسم/مستخدم/بريد) و`active` و`roleId` ← مستخدمون كاملون | |
| `GET /users/{id}` | مسجّل | كامل لصاحب `permissions.assign` وإلا مختصر (R20) | ETag |
| `POST /users` | `permissions.assign` | `{ username, name, email, title?, managerId?, systemRoleIds[], password }` ← 201 | R5، R7، R12، R14، R19؛ `mustChangePassword=true` |
| `PUT /users/{id}` | `permissions.assign` | `{ name, email, title, managerId }` | If-Match، R7، R14 |
| `PUT /users/{id}/roles` | `permissions.assign` | `{ systemRoleIds[] }` | If-Match، R10–R13، R19 |
| `POST /users/{id}/activate` | `permissions.assign` | 204 | If-Match، R11 |
| `POST /users/{id}/deactivate` | `permissions.assign` | 204 | If-Match، R3، R9، R11، R13 |
| `POST /users/{id}/reset-password` | `permissions.assign` | `{ newPassword }` ← 204 | R5، R11؛ بيصفّر القفل ويلغي جلساته ويحطّ `mustChangePassword=true` |

الأخطاء كلها problem+json بالعربي بالصيغة المتفق عليها بالجزء 00 (400/401/403/404/409/412/422/428/429).
`POST /auth/login` و`POST /auth/refresh` بس `AllowAnonymous`؛ كل شي تاني تحت `/api/v1` بيحتاج توكن.

## 6. الإعدادات
| المفتاح | القيمة |
|---|---|
| `Jwt:Issuer`، `Jwt:Audience`، `Jwt:SigningKey` (≥32 بايت؛ بالإنتاج من متغير بيئة، والإقلاع بيفشل إذا فاضي) | — |
| `Jwt:AccessTokenMinutes` / `Jwt:RefreshTokenDays` | 15 / 7 |
| `RateLimiting:AuthPerMinute` | 10 |
| `ForwardedHeaders:Enabled` | `false` (يتفعّل خلف proxy، مؤجَّل من الجزء 00) |
| `Seed:AdminUsername`، `Seed:AdminEmail`، `Seed:AdminPassword` | مطلوبة بالإنتاج |
| `Seed:Demo`، `Seed:DemoPassword` | تطوير فقط |

## 7. البيانات الأولية
- **دايماً:** الأدوار السبعة المدمجة (مدير النظام `system_admin`، مكتب المشاريع `pmo`، موظف `staff`، ومدير مشروع `project_manager`، معتمد `approver`، عضو فريق `team_member`، قارئ `viewer`) بصلاحياتها من `ipm-web/src/app/mock/db/seed.ts`،
  ومستخدم مدير واحد من الإعدادات (بيجبر على تغيير كلمة السر، R6). الـ seeder idempotent وبيعيد مزامنة صلاحيات `system_admin`.
- **بالتطوير فقط (`Seed:Demo`):** المستخدمون السبعة من الواجهة (`admin`، `khaled`، `noura`، `fahad`، `reem`، `abdullah`، و`mona` معطّلة) بنفس الأدوار وسلسلة المدراء، كلمة سر تجريبية من الإعدادات وبدون `mustChangePassword`.

## 8. سيناريوهات القبول
1. المدير بيسجّل دخول بكلمة السر الأولى ← `mustChangePassword=true`؛ أي رابط تاني ← 403 `Auth.PasswordChangeRequired`؛ بعد `POST /auth/password` بيشتغل و`/auth/me` بيرجّع كل المفاتيح الـ 42.
2. أي رابط بدون توكن ← 401 (ما عدا `/auth/login` و`/auth/refresh` و`/health` و`/docs`).
3. مستخدم دوره `staff` بيحاول `POST /roles` ← 403، وبيظهر `permission-denied` بـ `security_events`.
4. المدير بيشيل صلاحية من دور، والمستخدم اللي عليه بياخد 403 **بالطلب اللي بعده** بدون إعادة دخول.
5. منع صريح (`denied`) بدور تاني يعلو المنح من دور أول.
6. 5 محاولات غلط ← 6th بكلمة صح ← 429 `Auth.LockedOut`؛ ومحاولة باسم مجهول بترجّع نفس رسالة 401 تبع كلمة غلط.
7. refresh بيرجّع توكن جديد، واستعمال القديم مرة تانية بيلغي كل الجلسات (R4).
8. تعطيل مستخدم ← refresh تبعه بيفشل، وتوكن الوصول الجاري بيرجّع 401 بالطلب الجاي (R3).
9. R5 كلمة سر ضعيفة ← 400 بحقل `newPassword`؛ R7 اسم/بريد مكرر ← 409؛ R9، R10، R11، R12، R13 كل وحدة بخطئها.
10. R14: مدير مو نشط / نفسه / حلقة دائرية ← 400.
11. R15–R18 على الأدوار: حذف مدمج 422، حذف دور مسند 409، اسم مكرر 409، مفتاح مجهول أو منح+منع 400، تغيير `scope` 422.
12. `GET /users/directory` لموظف عادي بدون بريد ولا أدوار ولا مدير (R20)، و`GET /users` له ← 403.
13. تعديل مستخدم أو دور بـ ETag قديم ← 412، وبدون `If-Match` ← 428.
14. كل تعديل بيظهر بـ `audit_log` مع `user_id` الحقيقي، وبدون `password_hash` وأي سر.
15. `dotnet test` ناجح، والروابط ظاهرة بـ `/docs`، و`docs/http/01-identity.http` يسجّل دخول ويخزّن التوكن.

## 9. قرارات
**حسمها صاحب المشروع (الخارطة):**
- المستخدم هو نفسه `IdentityUser` (جدول واحد)، والصلاحيات مفاتيح ثابتة بالكود، تُمنح على مستوى النظام أو داخل مشروع، مع منع صريح (default-deny).

**أخدتها أنا (صحّحني إذا لازم):**
- **الدخول باسم المستخدم (`username`)** مش بالبريد، لأن الواجهة (`LoginRequest`) هيك. البريد بيضل فريد ومعلومة اتصال.
- **جلسة = access JWT 15 دقيقة + refresh cookie 7 أيام (HttpOnly, rotation)** حسب بنية الـ ERP، بدل التوكن الواحد اللي بالـ mock. الواجهة بتحتاج تعديل بسيط لما ننتقل للـ API الحقيقي (خارج شغلنا).
  وإعداد «مدة الجلسة» (`sessionHours` بالإعدادات العامة) بيتأجّل للجزء 09؛ هون القيمة من `appsettings`.
- **إعادة تعيين كلمة السر:** المدير بيحط كلمة مؤقتة (والمستخدم مجبور يغيّرها)، مش «رابط بالإيميل» متل الـ mock، لأنه ما في خادم بريد. لا تُرجَع كلمة السر بأي رد ولا بالتدقيق.
- **`permissions.assign` وحدها كافية لإدارة المستخدمين** (الـ mock كان يطلب `roles.manage` معها لإنشاء مستخدم). السبب: الرابط بيعلن مفتاح واحد؛ وسقف الامتيازات (R11/R12) بيمنع التصعيد أصلاً.
- **`security_events` جدول مستقل** (مش `audit_log`) لأن أحداث الدخول مش تغيير كيانات؛ الجزء 08 بيعرضه ضمن سجل النشاط والتدقيق.
- **القفل بالحساب** (Identity Lockout)، مش بالحساب+العميل متل الـ mock، مع تحديد معدل لكل IP، وسقف عام يحمي من التخمين الموزّع (حد 100 فشل/15 دقيقة على النظام كله ← 429).
- **قراءة الأدوار والصلاحيات والدليل لأي مستخدم مسجّل** (الواجهة بتحتاجها للاختيار).
- **ما في حذف للأدوار المدمجة ولا للمستخدمين** (R8، R15).

## 10. مش ضمن الجزء
أعضاء المشاريع وأدوارهم داخل المشروع (الجزء 04)، قراءة سجل النشاط والتدقيق (08)، إعدادات الجلسة العامة (09)،
تسجيل الدخول الموحّد SSO (FR-AUTH-07، أولوية لاحقة)، «نسيت كلمة السر» بالبريد، المصادقة الثنائية.

## 11. تعديلات أثناء التنفيذ
غيّرتها أثناء المراحل 1–4 بدون ما تغيّر الاتفاق، وكلها موثّقة هون:
1. **أدوار المستخدم عمود `role_ids uuid[]` بجدول `users`** بدل جدول `user_roles` (نفس منطق `permissions text[]` بالأدوار): تعيين الدور بيغيّر `xmin` (ETag) وبيظهر بـ `audit_log` بدون جدول مساعد. صحة الأدوار (موجودة، نطاق `system`) بيتحقق منها التطبيق (R19)، وحذف دور مسند بيتمنع (R16).
2. **`refresh_tokens` بدون `family_id`**: إعادة استعمال توكن قديم بتلغي كل جلسات المستخدم (R4)، فما في حاجة لعائلة توكنات.
3. **`POST /auth/password` بيرجّع 200 مع جلسة جديدة** (access token + cookie) بدل 204، لأن تغيير كلمة المرور بيبدّل الـ security stamp وبيبطّل التوكن الحالي؛ هيك الواجهة بتكمل بدون تسجيل دخول تاني.
4. **التفعيل والتعطيل (`activate` / `deactivate`) بيرجّعوا 200 مع المستخدم وETag جديد** بدل 204، حتى ما تحتاج الواجهة قراءة تانية قبل التعديل اللاحق. `reset-password` بيضل 204.
5. **اسم الدور فريد بدون حساسية حالة بالتطبيق (R17)**؛ الفهرس الفريد بالقاعدة حساس للحالة (يمسك التكرار الحرفي بحالة السباق).
6. **R13** بتنفحص قبل الحفظ بصورة بالذاكرة للمستخدمين والأدوار (عددهم صغير بنظام داخلي).
7. **ترتيب الأخطاء بالأدوار:** مفتاح صلاحية مجهول أو منح+منع (R18) ← 400 قبل فحص «ما بتملكها» (R12) ← 403.
8. **السيناريو 1:** مدير التطوير (`Seed:Demo`) ما بيطلب تغيير كلمة السر؛ الاختبار بيعمل نفس السيناريو بمستخدم جديد أنشأه المدير. خارج التطوير (`Seed:Demo=false`) المدير المزروع بيطلب تغييرها (R6).
9. **حد الفشل العام (100/15 دقيقة)** عدّاد بالذاكرة لكل نسخة من الـ API؛ كافي لخادم واحد، وبينتقل لـ Redis إذا صار في أكثر من نسخة.
10. **`GET /users/directory`** بيرجّع مصفوفة وحدة (بدون ترقيم) لأنه دليل للاختيار.
