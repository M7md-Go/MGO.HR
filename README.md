# Modern Time — HR & Attendance Management System

نظام متكامل لإدارة الموظفين والحضور والانصراف والورديات والإجازات والتقارير (عربي RTL).
[![Build](https://github.com/M7md-Go/MGO.HR/actions/workflows/build.yml/badge.svg)](https://github.com/M7md-Go/MGO.HR/actions/workflows/build.yml)

## البيئة التقنية

- C# 5.0 / .NET Framework 4.0 Client Profile / Visual Studio 2012
- Windows Forms — x86 (32-bit)
- Microsoft Access MDB عبر Jet OLEDB 4.0 (بدون كلمة مرور، بدون ACCDB)
- بدون EF/ORM ومكتبات خارجية؛ دعم أجهزة ZKTeco عبر `zkemkeeper.dll`

## البنية

```
HR.UI → HR.BLL → HR.DAL → HR.Entities
                 + HR.Helpers + HR.Services + HR.Reports
```

## البناء والتشغيل

1. افتح `ModernTime.sln` في Visual Studio 2012 أو أحدث.
2. اجعل Configuration = Release / Platform = x86، ثم Build.
3. شغّل `HR.UI\bin\Release\HR.UI.exe` — تُنشأ قاعدة البيانات تلقائيًا من
   `Database\ModernTime_Schema.sql` عند أول تشغيل مع مستخدم Admin افتراضي.

المتطلب على جهاز العميل: .NET Framework 4.0 (Client Profile) — موجود افتراضيًا
في Windows 7/8/10. محرك Jet 4.0 موجود افتراضيًا في ويندوز 32-بت؛ وعلى ويندوز
64-بت يعمل التطبيق كـ x86 (وهي بنية المشروع الافتراضية).

## البناء الآلي (GitHub Actions)

يُبنى الحل على Windows عند كل Push/PR، ويُرفع `ModernTime.zip` كـ Artifact.
عند إنشاء Tag باسم `v…` يُنشر Release مرفقًا به الإخراج.

## الوثائق

- `Docs/` — حالة المراحل 1–13 وقرارات التصميم
- `Docs/TEST_SCENARIOS.md` — سيناريوهات اختبار قواعد العمل الـ 15
- `Database/ModernTime_Schema.sql` — مخطط قاعدة البيانات الكامل (36 جدولًا)
