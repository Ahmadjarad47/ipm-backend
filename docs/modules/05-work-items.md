# الجزء 05: عناصر العمل — المواصفة

> المرحلة 0 من [خط الإنتاج](../MODULE-PIPELINE.md). الحالة: **مُسلَّم**.
> يعتمد على: [الجزء 02](02-work-item-types.md) و[03](03-workflows.md) و[04](04-projects.md) (مُسلَّمين).
> المرجع (للفهم فقط): `ipm-web/src/app/domain/work-items/{work-item.models,work-item.api}.ts`، `mock/server/handlers/work-items.handlers.ts`
> (الإنشاء، التعديل، الأرشفة، الأب، الانتقال/الإغلاق/إعادة الفتح، تغيير النوع)، `mock/server/handlers/extras.handlers.ts` (`work-items/bulk`)،
> `mock/server/handlers/projects.handlers.ts` (`projectProgress`)، `mock/server/transitions.ts`، `mock/db/seed.ts`، والـ SRS FR-TSK وFR-REL-01/02/10 وFR-WF-05/08/10.

## 1. الهدف
عنصر العمل هو وحدة الشغل داخل المشروع (ميزة، مهمة، خطأ…): بنوعه وحقوله المخصصة وحالته ومسؤوليه وأبيه وأبنائه.
هالجزء بيفعّل كل اللي جهّزته الأجزاء السابقة: الأنواع (02) وسير العمل (03) والمشاريع والصلاحيات بالمشروع (04).
وبيطبّق عملياً: شجرة الأب والأبناء، انتقالات الحالة بكل شروطها، تغيير النوع بفحص التعارضات، التعديل الجماعي، ولوحة الكانبان، وتقدّم المشروع.
وبيفعّل **الحمايات المؤجَّلة من 02 و03 و04** (مثلاً: ما بتعطّل حالة عليها عناصر).

> **تقسيم:** الروابط بين العناصر (علاقات `blocks` وغيرها) والتعليقات بجزء لحالهم، **05b**، حتى يضل كل جزء قابل للمراجعة (متل ما عملنا بالـ ERP). هون ما في علاقات، فشرط `blockedByOpenDependencies` بالانتقال ما بيلاقي ما يمنعه، وبيتفعّل فعلياً مع 05b بدون تغيير بالانتقال نفسه.
> المرفقات (رفع الملفات وتخزينها) كمان لجزء لحاله بآخر الخارطة (الملفات).

## 2. الجداول (schema `work`)
| الجدول | الأعمدة | القيود |
|---|---|---|
| `work_items` | `id`، `project_id`، `number`، `ref`، `type_id`، `title`، `description`، `status_id`، `priority`، `start_date`، `due_date`، `parent_id`، **`assignee_ids uuid[]`**، **`fields jsonb`** (`{ key: value }`)، `is_archived` + الأختام + `xmin` | `(project_id, number)` فريد؛ `ref` فريد (`CODE-n`)؛ `title` 1–200؛ `priority` من `low medium high critical` (CHECK)؛ `due_date ≥ start_date` عند وجودهما (CHECK)؛ `parent_id` FK ذاتي (`RESTRICT`) و`parent_id ≠ id`؛ FK للمشروع والنوع والحالة؛ فهارس: `(project_id, status_id)`، `parent_id`، GIN على `assignee_ids`، `(project_id, is_archived)` |
| `work_item_status_changes` | `id`، `work_item_id`، `from_status_id`، `to_status_id`، `actor_id`، `comment`، `at`، `approval_overridden` | append-only؛ فهرس `(work_item_id, at)` |
| `project_counters` | `project_id`، `last_number` | PK على المشروع؛ بيعطي رقم العنصر التالي بعملية ذرّية داخل نفس المعاملة (بدون تضارب بين طلبين متزامنين) |

- **الرمز `ref` = رمز المشروع + `-` + الرقم** (`ERP-104`)، بيتولّد عند الإنشاء وبيضل ثابت (رمز المشروع ما بيتغيّر). **الترقيم بيبدأ من 101** (متل بيانات الواجهة).
- **الحقول المخصصة والمسؤولون أعمدة داخل العنصر** (jsonb و`uuid[]`) مش جداول: تعديلهم بيغيّر `xmin` (ETag) وبيظهر كامل بـ `audit_log`، والاستعلام «مسؤول عنه فلان» بفهرس GIN.
- **عنصر العمل ما بينحذف، بيتأرشف** (R3). المؤرشف للقراءة فقط ومخفي من القوائم الافتراضية.
- حالة العنصر: `status_id`، بتتغيّر بانتقال فقط (R5). سير العمل الفعلي = استثناء المشروع للنوع إن وُجد وإلا سير عمل النوع (`Project.EffectiveWorkflowId` الموجودة من 04).
- المعرّفات UUIDv7، والـ enums نص kebab-case.

## 3. الصلاحيات
الفحص **بالمعالج** (بالمشروع، متل 04): مشروع غير مرئي ← **404**، مرئي بدون الصلاحية ← **403** (ويتسجّل `permission-denied`).
| الصلاحية | نطاقها | بتفتح |
|---|---|---|
| `tasks.view` | مشروع/نظام | قراءة العناصر والقائمة واللوحة وسجل الحالة |
| `tasks.create` | مشروع | إنشاء عنصر |
| `tasks.edit` | مشروع | تعديل البيانات والحقول، وتغيير الأب |
| `tasks.assign` | مشروع | تعيين المسؤولين (عند الإنشاء والتعديل والجماعي) |
| `tasks.delete` | مشروع | أرشفة عنصر |
| `workflow.transition` | مشروع | انتقال الحالة والإغلاق وإعادة الفتح (+ `permission` الإضافية للانتقال إن وُجدت) |
| `workitem.types.manage` | مشروع | تغيير نوع عنصر (والفحص قبله) |
| `audit.override` | نظام | تنفيذ انتقال يتطلب موافقة قبل ما ينبني محرك الموافقات (الجزء 06) |
| `workitem.types.manage` / `statuses.manage` | نظام | معاينة الأثر (`impact`) لنوع / لحالة |

## 4. قواعد العمل
| # | القاعدة | الخطأ |
|---|---|---|
| R1 | **الإنشاء:** المشروع غير مؤرشف، النوع مفعّل ومسموح بالمشروع، العنوان 1–200، التواريخ صالحة والاستحقاق ≥ البدء، الأولوية افتراضية `medium`، والحالة الأولية = الابتدائية لسير العمل الفعلي (ما بنختار حالة) | 422 `WorkItem.ProjectArchived` / `WorkItem.TypeNotAllowed`، 400 (حقل) |
| R2 | **التعديل** (`PUT`، If-Match): العنوان والوصف والأولوية والتواريخ والمسؤولون والحقول. تعديل غير المسؤولين بيحتاج `tasks.edit`، وتغيير المسؤولين بيحتاج `tasks.assign`. المؤرشف أو بمشروع مؤرشف للقراءة فقط. الحالة والنوع والأب ما بتتغيّر من هون | 422 `WorkItem.Archived` / `WorkItem.ProjectArchived` / `WorkItem.StatusChangeNotAllowed` |
| R3 | **الأرشفة** (`DELETE`): بدون أبناء نشطين (بيتحمّل المستخدم نقلهم أولاً، ولا حذف جماعي صامت للأبناء) | 422 `WorkItem.HasActiveChildren` |
| R4 | **المسؤولون:** مستخدمون **نشطون وأعضاء** بالمشروع، بدون تكرار. (مسؤول سابق لم يعد عضواً بيضل ما لم يُعدَّل بالقائمة) | 400 `WorkItem.AssigneesInvalid` |
| R5 | **الحقول المخصصة** بتتحقق بتعريف النوع: المطلوب ما ينترك فاضي، `number` ضمن `min/max`، `select` من خيارات النوع، `date` بصيغة `yyyy-MM-dd`، `boolean`، `user` مستخدم موجود، `text` حتى 2000 حرف، ومفتاح غير معرّف بالنوع مرفوض (إلا إذا كان قيمة محفوظة سابقاً لحقل انحذف تعريفه) | 400 `WorkItem.FieldsInvalid` + `errors` بمسار `fields.<key>` |
| R6 | **الأب:** نفس المشروع، ونوع العنصر ضمن `allowedChildTypeIds` لنوع الأب (02)، وبدون حلقة (لا نفسه ولا أحد أحفاده). تغييره بـ `PUT …/parent` (If-Match، `tasks.edit`) و`null` يشيله | 422 `WorkItem.ParentDifferentProject` / `WorkItem.ParentTypeNotAllowed` / `WorkItem.ParentCycle` / `WorkItem.ParentArchived` |
| R7 | **الانتقال** (`POST …/transition`، If-Match، `workflow.transition`): معرّف بسير العمل الفعلي، الحالة الهدف مفعّلة، `permission` الإضافية متوفرة بالمشروع، `requiresAssignee` ← لازم مسؤول، `blockedByOpenDependencies` ← ما في حاجب مفتوح (**يتفعّل مع 05b**). المؤرشف لا ينتقل | 422 `WorkItem.TransitionNotAllowed` / `WorkItem.TargetStatusInactive` / `WorkItem.AssigneeRequired` / `WorkItem.BlockedByDependencies`، 403 |
| R8 | `requiresApproval` ← طلب موافقة معتمد (**الجزء 06**). لحد ما ينبني: بيتنفّذ فقط من معه `audit.override` وبيتسجّل بسجل الحالة `approvalOverridden = true` (متل 04) | 422 `WorkItem.ApprovalRequired` |
| R9 | **الإغلاق** (`POST …/close`): بيروح لحالة من تصنيف `done` معرّفة من الحالة الحالية؛ إذا فيه وحدة بس بتنختار تلقائياً، وإذا أكتر لازم `toStatusId`. **إعادة الفتح** (`POST …/reopen`): للمنجز أو الملغى فقط، **السبب (`comment`) إلزامي**، والهدف حالة غير مغلقة. كلاهما بنفس شروط R7 وR8 | 400 `WorkItem.CloseTargetRequired` / `WorkItem.ReopenReasonRequired`، 422 `WorkItem.CloseTargetInvalid` / `WorkItem.NotClosed` |
| R10 | **تغيير النوع** (`POST …/type-change/check` بدون تغيير، ثم `POST …/type-change` If-Match): بيحتاج `workitem.types.manage` بالمشروع، وبيرجّع **كل** التعارضات بدل ما يفقد بيانات بصمت: النوع غير مسموح أو معطّل، الحالة غير موجودة بسير عمل النوع الجديد، حقول مطلوبة ناقصة أو غير صالحة، حقول ستُفقد، نوع الأب أو الأبناء ما بيسمحوا بالنوع الجديد | 422 `WorkItem.TypeChangeConflicts` مع `conflicts: [{ field, reason }]` |
| R11 | **الكانبان** (`GET /work-items/board?projectId=`): كل العناصر غير المؤرشفة بشكل خفيف مع سير العمل الفعلي والحقول المعلَّمة `showOnCard` فقط | — |
| R12 | **التعديل الجماعي** (`POST /work-items/bulk`، حتى 100): `assign` أو `priority` أو `dueDate` أو `transition`؛ كل عنصر بيتفحص لحاله بنفس قواعده وصلاحياته، والرد نتيجة لكل عنصر `{ id, ref, ok, message }` (مش كله أو لا شي) | 400 `Bulk.Empty` / `Bulk.TooMany` |
| R13 | **تقدّم المشروع** (بيرجع مع المشروع): التقدم = العناصر **الورقية** المنجزة ÷ الورقية المحتسبة (الأب ما بيتحسب مع أبنائه)، بتُستثنى الملغاة والمؤرشفة، والموافقة لا تعني الإنجاز، و`percent = null` بدون ورقيات؛ مع `workItemCount` و`overdueCount` (متأخر = استحقاق قبل اليوم وغير منجز أو ملغى) | — |
| R14 | **حمايات الأجزاء السابقة (تتفعّل هون):** تقليص الأنواع المسموحة بالمشروع تحت عناصر من نوع مشطوب (04 R17) ← `Project.TypesInUse`؛ استثناء سير عمل لا يدعم حالات عناصر قائمة ← `Project.WorkflowOverrideConflicts`؛ إزالة حالة عناصر من سير عمل عناصر، أو تعطيلها وعليها عناصر (03 R15) ← `Workflow.StatusInUse` / `Status.InUse`؛ تغيير سير عمل نوع بعناصر بحالات ما بيدعمها ← `WorkItemType.WorkflowChangeConflicts`. وإزالة عضو مسؤول عن عناصر مفتوحة بترجّع `warnings` (04 R17) | 422 |
| R15 | **معاينة الأثر:** `GET /work-item-types/{id}/impact` و`GET /statuses/{id}/impact` للقراءة فقط قبل التعطيل أو تغيير الربط | — |

## 5. الروابط (`/api/v1`، مجموعة: «عناصر العمل»)
| الرابط | الصلاحية | الطلب ← الرد | ملاحظات |
|---|---|---|---|
| `GET /work-items` | `tasks.view` (بالمشاريع المرئية) | ترقيم + فلاتر ← عناصر | الفلاتر: `projectId` `typeId` `statusId` `priority` `assigneeId` `unassigned` `parentId` `hasChildren` `q` (عنوان/ref) `overdue` `includeArchived` `createdFrom/To` `dueFrom/To`؛ الترتيب: `ref` `title` `priority` `dueDate` `status` `type` `createdAt` |
| `GET /work-items/board` | `tasks.view` | `?projectId=` ← عناصر خفيفة | R11 |
| `GET /work-items/{id}` | `tasks.view` | ← العنصر كامل | ETag |
| `POST /work-items` | `tasks.create` (+ `tasks.assign` إذا فيه مسؤولين) | `{ projectId, typeId, title, description?, priority?, startDate?, dueDate?, assigneeIds?, parentId?, fields? }` ← 201 | R1، R4–R6 |
| `PUT /work-items/{id}` | `tasks.edit` / `tasks.assign` | `{ title, description, priority, startDate, dueDate, assigneeIds, fields, statusId?, typeId?, parentId? }` | If-Match؛ R2؛ الثلاثة الأخيرة مجرد فحص (غير المختلفة بتنقبل) |
| `DELETE /work-items/{id}` | `tasks.delete` | 204 | If-Match؛ R3 (أرشفة) |
| `PUT /work-items/{id}/parent` | `tasks.edit` | `{ parentId: Guid? }` | If-Match؛ R6 |
| `POST /work-items/{id}/transition` | `workflow.transition` | `{ toStatusId, comment? }` | If-Match؛ R7، R8 |
| `POST /work-items/{id}/close` | `workflow.transition` | `{ toStatusId?, comment? }` | If-Match؛ R9 |
| `POST /work-items/{id}/reopen` | `workflow.transition` | `{ toStatusId?, comment }` | If-Match؛ R9 |
| `GET /work-items/{id}/status-history` | `tasks.view` | ترقيم ← `[{ at, fromStatusId, toStatusId, actorId, actorName, comment, approvalOverridden }]` | الأحدث أولاً |
| `POST /work-items/{id}/type-change/check` | `workitem.types.manage` | `{ typeId }` ← `{ conflicts: [...] }` | R10 (للقراءة فقط) |
| `POST /work-items/{id}/type-change` | `workitem.types.manage` | `{ typeId }` | If-Match؛ R10 |
| `POST /work-items/bulk` | حسب كل إجراء | `{ ids[], action: { kind, … } }` ← `[{ id, ref, ok, message }]` | R12 |
| `GET /work-item-types/{id}/impact` | `workitem.types.manage` | ← `{ items, projects, workflows, asDefaultIn:[{id,name}] }` | R15 |
| `GET /statuses/{id}/impact` | `statuses.manage` | ← نفس الشكل | R15 |

وبيتغيّر رد **المشروع** (إضافة فقط): `progress { percent, doneLeaves, countedLeaves, rule }` و`workItemCount` و`overdueCount` (R13).
شكل العنصر: `{ id, ref, projectId, typeId, title, description, statusId, priority, assigneeIds, startDate, dueDate, parentId, fields, archived, createdBy, createdAt, version }`.
الأخطاء problem+json بالعربي بالصيغة المتفق عليها بالجزء 00. التواريخ `yyyy-MM-dd`، والأولوية kebab-case بالـ JSON.

## 6. البيانات الأولية
- **دايماً:** ما في (العناصر بيانات تشغيلية).
- **بالتطوير فقط (`Seed:Demo`):** عناصر العمل التجريبية من `seed.ts` بمشاريعها (ERP وSSP وBI وMAIL) بأنواعها وحالاتها وأولوياتها ومسؤوليها وشجرة آبائها وحقولها المخصصة وتواريخها النسبية لليوم؛ أرقامها `ERP-101…`. بتنزرع مباشرة بحالتها (بدون موافقة ولا سجل انتقال).

## 7. سيناريوهات القبول
1. `khaled` (مدير ERP) بيقرا قائمة عناصر ERP بالترقيم والترتيب؛ `abdullah` (قارئ) بيقرا؛ مستخدم ما هو عضو بالمشروع ← 404 على كل عنصر؛ بدون توكن ← 401.
2. **الإنشاء (R1):** `fahad` (عضو فريق) بينشئ مهمة بحالة `جديد` ورقم `ERP-` التالي (أكبر من 101)، النوع غير المسموح بالمشروع أو معطّل ← 422، العنوان الفاضي ← 400، `abdullah` (بدون `tasks.create`) ← 403، بمشروع مؤرشف ← 422.
3. **الترقيم:** إنشاء عدة عناصر متزامنة بنفس المشروع ما بيكرر رقماً ولا بيفشل بسبب التضارب.
4. **المسؤولون (R4):** `tasks.assign` شرط التعيين؛ عضو غير نشط أو غير عضو بالمشروع ← 400؛ بدون التعيين بيضل بدون مسؤول.
5. **الحقول (R5):** نوع «خطأ» بدون `severity` المطلوبة ← 400 `fields.severity`؛ قيمة `select` غلط، `number` خارج `min/max`، مفتاح مجهول ← 400 بمساره؛ حقل مخصص صالح بينحفظ وبيرجع.
6. **التعديل (R2):** `fahad` بيعدّل العنوان والأولوية (If-Match)؛ تغيير المسؤولين بدون `tasks.assign` ← 403؛ `statusId` مختلف بالـ PUT ← 422؛ ETag قديم ← 412، بدون If-Match ← 428.
7. **الأب (R6):** مهمة تحت ميزة ← 200؛ نوع غير مسموح (ميزة تحت مهمة) ← 422؛ مشروع ثاني ← 422؛ حلقة (أب يصير ابن لحفيده) ← 422؛ `null` يشيل الأب.
8. **الأرشفة (R3):** عنصر له أبناء نشطين ← 422؛ بعد نقل الأبناء ← 204 ويختفي من القائمة الافتراضية ويظهر بـ `includeArchived=true`، ومؤرشف ما بيتعدّل (422).
9. **الانتقال (R7):** `جديد ← قيد التنفيذ` بدون مسؤول ← 422 `AssigneeRequired` وبمسؤول ← 200 ويظهر بسجل الحالة؛ انتقال مش معرّف، لحالة معطّلة ← 422؛ انتقال له `permission` إضافية ← 403 لمن ما عنده هالصلاحية بالمشروع؛ بمشروع مؤرشف ← 422.
10. **الإغلاق/إعادة الفتح (R9):** `close` من `قيد المراجعة` بيروح `مكتمل` تلقائياً؛ `reopen` بدون سبب ← 400؛ لعنصر غير مغلق ← 422.
11. **الموافقة (R8):** انتقال `requiresApproval` ← 422 لغير `audit.override`، ومعه ← 200 و`approvalOverridden`.
12. **تغيير النوع (R10):** `check` بيرجّع التعارضات بدون تغيير (حقل مطلوب ناقص، حقل سيُفقد، حالة غير موجودة بسير عمل النوع الجديد، نوع ابن غير مسموح)؛ التطبيق مع تعارض ← 422 بالقائمة؛ بدون تعارض ← 200 وETag جديد؛ بدون `workitem.types.manage` ← 403.
13. **الجماعي (R12):** `assign` و`priority` و`dueDate` و`transition` لعدة عناصر: النتيجة لكل عنصر، عنصر ما بيسمح له الانتقال ما بيوقف الباقي؛ أكثر من 100 ← 400؛ فاضي ← 400.
14. **اللوحة (R11):** كل عناصر المشروع غير المؤرشفة بشكل خفيف، والحقول `showOnCard` فقط.
15. **التقدم (R13):** `GET /projects/{id}` بيرجّع `progress` (الورقيات فقط، الملغاة والمؤرشفة مستثناة) و`workItemCount` و`overdueCount`؛ مشروع بدون عناصر ← `percent = null`.
16. **حمايات (R14):** تعطيل حالة عليها عناصر ← 422 `Status.InUse`؛ إزالة حالة مستعملة من سير عمل ← 422؛ تقليص أنواع المشروع تحت عناصر ← 422؛ استثناء سير عمل بحالات ناقصة ← 422؛ إزالة عضو مسؤول عن عنصر مفتوح بتنجح وبترجّع `warnings`.
17. **الأثر (R15):** `impact` بيرجّع عدد العناصر والمشاريع والأنواع/الحالات الافتراضية وبصلاحيته فقط.
18. كل تعديل بيظهر بـ `audit_log` مع `user_id`؛ `dotnet test` ناجح؛ الروابط ظاهرة بـ `/docs` تحت «عناصر العمل»؛ و`docs/http/05-work-items.http` فيه مثال لكل رابط.

## 8. قرارات
**حسمها صاحب المشروع (الخارطة):** عناصر العمل بعد المشاريع (04) وقبل الموافقات (06). الوحدة والترتيب من الخارطة.

**أخدتها أنا (صحّحني إذا لازم):**
- **تقسيم 05 على 05 و05b:** الروابط والتعليقات بجزء لحاله (متل 05 و05b بالـ ERP)، لأنها قواعد مستقلة (دورات، أزواج أنواع، تحرير بنافذة زمنية) وحجم هالجزء كبير أصلاً. الأثر الوحيد: شرط `blockedByOpenDependencies` ما بيشتغل فعلياً قبل 05b (لأنه ما في علاقات). **وكمان نقلت المرفقات** (رفع الملفات والتخزين) لجزء الملفات بآخر الخارطة. **العروض المحفوظة** (`views-menu`) تخزين محلي بالمتصفح بالواجهة، مش مسؤولية الخادم.
- **ترقيم بعدّاد ذرّي بجدول `project_counters`** مش `MAX(number)+1`، حتى ما يتكرر رقم ولا يتضارب طلبين متزامنين، وبيبدأ من 101.
- **الأرشفة بدل الحذف ولا استعادة** (متل الـ mock). إذا بدك «استعادة» بنضيفها بسطر.
- **الانتقال اللي بيتطلب موافقة قبل 06:** نفس قاعدة المشاريع (04): بصلاحية `audit.override` فقط وبيتسجّل.
- **`text` بالحقل المخصص حتى 2000 حرف، والوصف حتى 20000**، لحماية الجدول من نصوص ضخمة.
- **مفتاح حقل غير معرّف بالنوع مرفوض** (الـ mock كان يقبله ويخزّنه)، حتى ما تتراكم بيانات يتيمة؛ المستثنى القيم القديمة لحقول انحذف تعريفها.
- **قراءة العنصر بتتطلب `tasks.view` وحده** (403 بدونها بمشروع مرئي)، متل الـ mock.
- **المنطقة الزمنية للمتأخر:** `Asia/Riyadh` (الافتراضية بإعدادات الواجهة) كثابت لحد ما ينبني إعداد المؤسسة بالجزء 09.
- **لا قائمة «كل عناصري عبر المشاريع» منفصلة:** `GET /work-items?assigneeId=<me>` بيغطيها عبر كل المشاريع المرئية.
- **ما في إشعارات هلق** (تكليف، تغيّر حالة): بتجي بالجزء 08 من أحداث المجال؛ والمعالج بيضل جاهز لإطلاقها.

## 9. نقاط ربط للأجزاء الجاية (مش ضمن هالجزء)
| الجزء | بيضيف |
|---|---|
| 05b العلاقات والتعليقات | علاقات العناصر (`blocks` وغيرها: دورات، أزواج، مكرر، بين مشاريع)، قفل خصائص نوع الرابط بعد الاستعمال (02 R9) ومنع روابط جديدة من نوع معطّل (02 R10)، شرط `blockedByOpenDependencies`، فلاتر `hasDependency`/`unblocked`، والتعليقات على المشاريع والعناصر |
| 06 محرك الموافقات | استبدال استثناء `audit.override` بطلب موافقة معتمد، وإبطال الطلبات المعلّقة عند تغيير حقل محمي (أب، نوع، علاقات) |
| 07 تكامل Git | مؤشر التطوير (`dev`) بالعنصر، وربط `PROJ-n` من الـ commits |
| 08 النشاط والتدقيق | الإشعارات (تكليف، تغيّر حالة)، وشاشة النشاط لكل عنصر |
| 09 اللوحات والإعدادات | البحث الشامل، فلتر الأنواع المحتسبة بالتقدم، والمنطقة الزمنية والإعدادات العامة |
| الملفات (آخر الخارطة) | المرفقات على العناصر والمشاريع |

## 10. مش ضمن الجزء
العلاقات بين العناصر والتعليقات (05b)، المرفقات (الملفات)، الموافقات (06)، المستودعات (07)، الإشعارات وشاشة التدقيق (08)، البحث الشامل واللوحات (09)، العروض المحفوظة (الواجهة).

## 11. تعديلات أثناء التنفيذ
غيّرتها أثناء المراحل 1–4 بدون ما تغيّر الاتفاق، وكلها موثّقة هون:
1. **أخطاء الحقول المخصصة (R5)** بترجع 400 بمفاتيح `fields.<key>` (مثلاً `fields.severity`) بنفس صيغة أخطاء التحقق، وما إلها رمز خاص. `WorkItem.FieldsInvalid` بقي رمزاً احتياطياً بالمجال.
2. **رد تغيير النوع:** عند وجود تعارضات بيرجّع 422 `WorkItem.TypeChangeConflicts` ومعه مصفوفة `conflicts` من `{ field, reason }` بجسم الخطأ، والعنصر ما بيتغيّر. مسارات `field`: `typeId` و`statusId` و`parentId` و`children` و`fields.<key>`.
3. **الإغلاق وإعادة الفتح:** إعادة الفتح بدون `toStatusId` بتروح لأول حالة مفتوحة (بترتيب عرض الحالات) يسمح فيها سير العمل من الحالة الحالية، ولو ما في ولا وحدة ← 422 `WorkItem.ReopenTargetInvalid`. سبب إعادة الفتح الفاضي ← 400 (مفتاح `comment`).
4. **`PUT /work-items/{id}`:** الحقول `statusId` و`typeId` و`parentId` اختيارية لمجرد الفحص، والمختلف عن الحالي ← 422 برمز خاص لكل وحدة (`WorkItem.StatusChangeNotAllowed` و`TypeChangeNotAllowed` و`ParentChangeNotAllowed`). الطلب بدون أي تغيير بيحتاج `tasks.edit` برضو.
5. **اللوحة:** `GET /work-items/board` بيرجّع `{ projectId, workflows: [{ typeId, workflowId, statusIds }], items }` (سير العمل لكل نوع مسموح بالمشروع). عناصر نوع لم يعد مسموحاً بتضل ظاهرة بحقولها `showOnCard`.
6. **الترقيم:** العدّاد ذرّي بجملة `INSERT … ON CONFLICT … RETURNING`، ورقم أُعطي ثم فشل الحفظ بيتخطّاه العدّاد (فجوة بالأرقام مقبولة). الـ seed بيضبط العدّاد بعد آخر رقم زُرع.
7. **الأثر (R15)** بمساري `GET /work-item-types/{id}/impact` و`GET /statuses/{id}/impact` (صلاحيتا `workitem.types.manage` و`statuses.manage` بالنظام) والرد `{ items, projects, workflows, asDefaultIn: [{ id, name }] }`. للنوع `projects` هي المشاريع اللي بتسمح فيه، وللحالة هي المشاريع اللي فيها عناصر بالحالة (أو بهالحالة نفسها لحالات المشاريع).
8. **حمايات R14:** العدّ بيشمل العناصر المؤرشفة (بتضل مقروءة وحالتها لازم تضل موجودة)، ما عدا تحذيرات العضو المُزال فهي للعناصر المفتوحة غير المؤرشفة فقط. رموز جديدة: `Status.InUse` (بوصف العناصر) و`Workflow.StatusInUse` و`WorkItemType.WorkflowChangeConflicts` و`Project.TypesInUse` و`Project.WorkflowOverrideConflicts`.
9. **التعديل الجماعي:** كل عنصر بيتحفظ لحاله، والرد `[{ id, ref, ok, message }]` بدون فحص `If-Match`. نوع الإجراء بالحروف بالضبط: `assign` و`priority` و`dueDate` و`transition`.
10. **المتأخر والتقدم** بتوقيت `Asia/Riyadh` كثابت (UTC+3 بدون توقيت صيفي) بدل الاعتماد على tzdata بالحاوية. النسبة رقم صحيح مقرّب، ومعها `rule: "leaf-items-done-over-counted"`. الحقول الجديدة بتظهر بـ `GET /projects` و`GET /projects/{id}` فقط.
11. **اختبار من الجزء 04:** «تغيير الأنواع المسموحة يحتاج الصلاحية» كان بيضيّق أنواع ERP، ومع حماية R14 صار مرفوضاً لأن ERP فيه عناصر من كل الأنواع، فانتقل لمشروع جديد بدون عناصر.
12. **الـ seed:** معرّفات العناصر ثابتة (`…-8009-…`)، بتنزرع بالتطوير فقط، وبحالتها النهائية (بدون سجل حالة).
13. **الصلاحيات الافتراضية:** تغيير النوع بيحتاج `workitem.types.manage` بالمشروع، ودور «مدير مشروع» الافتراضي ما بيملكها، فهو لمدير النظام أو لدور مخصص.
