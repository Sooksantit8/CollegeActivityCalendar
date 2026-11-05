# College Activity Calendar

ระบบปฏิทินกิจกรรมของวิทยาลัย พัฒนาด้วย .NET 10, ASP.NET Core MVC, Razor View, jQuery, Bootstrap 5, FullCalendar, Entity Framework Core, ASP.NET Core Identity และ Microsoft SQL Server

## สิ่งที่ต้องมี

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Microsoft SQL Server 2019 ขึ้นไป (หรือ Docker สำหรับรัน SQL Server บนเครื่อง)
- เครื่องมือ EF Core: `dotnet tool install --global dotnet-ef`
- Visual Studio 2022 17.14 ขึ้นไป หรือ VS Code พร้อมส่วนขยาย C# Dev Kit

ไฟล์ `global.json` กำหนดให้โปรเจกต์ใช้ SDK 10 ตรวจสอบด้วย `dotnet --list-sdks`

## 1. เตรียม SQL Server

ถ้ามี SQL Server อยู่แล้ว ข้ามไปข้อ 2 ได้ หากต้องการรันด้วย Docker:

```bash
cp .env.example .env
```

แก้ `MSSQL_SA_PASSWORD` ใน `.env` แล้วสั่ง

```bash
docker compose up -d
```

บน Mac ชิป Apple Silicon หาก image หลักรันไม่ได้ ให้เพิ่มบรรทัด `MSSQL_IMAGE=mcr.microsoft.com/azure-sql-edge:latest` ใน `.env`

## 2. ตั้งค่า Connection String

ค่าอยู่ที่ `ConnectionStrings:DefaultConnection` เลือกวิธีใดวิธีหนึ่ง

**แนะนำสำหรับเครื่องนักพัฒนา (ไม่เก็บรหัสผ่านในซอร์สโค้ด):**

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=CollegeActivityCalendar;User Id=sa;Password=รหัสผ่านของคุณ;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

**หรือแก้ไฟล์ `appsettings.json`** ตัวอย่างค่าอื่น:

```text
Windows Authentication:
Server=.\SQLEXPRESS;Database=CollegeActivityCalendar;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True

LocalDB:
Server=(localdb)\mssqllocaldb;Database=CollegeActivityCalendar;Trusted_Connection=True;MultipleActiveResultSets=True
```

**หรือใช้ตัวแปรสภาพแวดล้อม** `ConnectionStrings__DefaultConnection` (เหมาะกับเซิร์ฟเวอร์จริง)

## 3. สร้างฐานข้อมูลด้วย Migration

Migration อยู่ใน `Data/Migrations` แล้ว สั่งสร้างฐานข้อมูลด้วย

```bash
dotnet ef database update
```

ฐานข้อมูลใช้ collation `Thai_CI_AS` เพื่อเรียงลำดับและค้นหาภาษาไทยได้ถูกต้อง

> ในโหมด Development ระบบรัน Migration ให้อัตโนมัติเมื่อเริ่มทำงาน (`Database:AutoMigrate`) จึงข้ามคำสั่งนี้ได้

เมื่อแก้ Entity ในอนาคต ให้สร้าง Migration ใหม่ด้วย `dotnet ef migrations add ชื่อการเปลี่ยนแปลง -o Data/Migrations`

## 4. เปิดใช้งานระบบ

```bash
dotnet run
```

เปิด <http://localhost:5166> หรือกด F5 ใน Visual Studio / VS Code

**VS Code:** เปิดโฟลเดอร์ `CollegeActivityCalendar` แล้วกด F5 (ใช้ค่าจาก `.vscode/launch.json`) ค่าเริ่มต้นใน `.vscode` ชี้ไปที่ .NET 10 SDK ใน `~/.dotnet10` หากติดตั้ง SDK ไว้ที่อื่น ให้แก้ path ใน `tasks.json`, `launch.json` และ `settings.json`

## 5. Seed Data และบัญชีตัวอย่าง

ระบบ Seed ทุกครั้งที่เริ่มทำงานและรันซ้ำได้โดยไม่สร้างข้อมูลซ้ำ

| การตั้งค่า | ความหมาย | Development | ค่าเริ่มต้น |
|---|---|---|---|
| `Seed:Enabled` | เปิดการ Seed | `true` | `true` |
| `Seed:DemoData` | สร้างบัญชี ห้องเรียน นักเรียน และกิจกรรมตัวอย่าง | `true` | `false` |
| `Seed:AdminEmail` / `Seed:AdminPassword` | บัญชีผู้ดูแลระบบเริ่มต้น | ใช้ค่าตัวอย่าง | ต้องกำหนดเอง |
| `Database:AutoMigrate` | รัน Migration ตอนเริ่มระบบ | `true` | `false` |

รหัสผ่านของบัญชีตัวอย่างทุกบัญชีคือ `College@2569`

| บทบาท | อีเมล | หมายเหตุ |
|---|---|---|
| Admin | `admin@college.example` | จัดการได้ทุกส่วน |
| Staff | `staff@college.example` | เจ้าหน้าที่งานกิจกรรม |
| Teacher | `teacher@college.example` | รับผิดชอบแผนก IT และคอมพิวเตอร์ธุรกิจ เป็นที่ปรึกษา ปวช.1 IT/1 |
| Teacher | `teacher2@college.example` | ดูแลการบัญชีเฉพาะ ปวช.1 ทุกห้อง และห้อง ปวส.1 ACC/1 |
| Student | `student@college.example` | นักเรียน ปวช.1 IT/1 |
| Student | `student02@college.example` ถึง `student21@college.example` | นักเรียนแผนกอื่น ๆ |

**ก่อนใช้งานจริง:** ตั้ง `ASPNETCORE_ENVIRONMENT=Production` ให้ `Seed:DemoData` เป็น `false` กำหนด `Seed:AdminPassword` ผ่านตัวแปรสภาพแวดล้อม แล้วเปลี่ยนรหัสผ่านหลังเข้าสู่ระบบครั้งแรก

## สิทธิ์การใช้งาน

| บทบาท | การมองเห็นกิจกรรม | การจัดการกิจกรรม | ส่วนอื่น |
|---|---|---|---|
| Admin | ทั้งหมด | ทั้งหมด | ผู้ใช้ โครงสร้างการศึกษา ข้อมูลหลัก เลื่อนชั้น รายงาน ตั้งค่า |
| Staff | ที่เผยแพร่แล้ว (ยกเว้นนัดหมายรายบุคคลของผู้อื่น) และที่ตนจัดการได้ | กิจกรรมระดับวิทยาลัย กิจกรรมที่ตนสร้าง หรือตนเป็นผู้รับผิดชอบ | - |
| Teacher | ที่เผยแพร่และเกี่ยวข้องกับตน และที่ตนจัดการได้ | กิจกรรมที่ตนสร้างหรือรับผิดชอบ และกิจกรรมที่เป้าหมายทั้งหมดอยู่ในแผนก/ห้อง/นักเรียนที่รับผิดชอบ กำหนดเป้าหมายระดับวิทยาลัยไม่ได้ | - |
| Student | เฉพาะที่เผยแพร่แล้วและตรงกับตน (วิทยาลัย แผนก ห้อง หรือรายบุคคล) | ไม่ได้ | ดูข้อมูลและประวัติของตน |

สิทธิ์ตรวจสอบ 3 ชั้น: `[Authorize]` บน Controller, `EventAccessService` ในชั้น Service (ทุกการอ่านและแก้ไขกิจกรรมผ่านจุดนี้) และเงื่อนไขใน Razor View สำหรับซ่อนเมนูและปุ่ม

กิจกรรมสถานะ **Scheduled** จะแสดงให้นักเรียนเห็นเมื่อถึงเวลาเผยแพร่ทันที และ `ScheduledEventPublisher` เปลี่ยนสถานะเป็น Published ทุก 1 นาที

## ขอบเขตที่ครูดูแล

กำหนดที่ **ผู้ใช้งาน → ครู → แก้ไข** ครูหนึ่งคนมีได้หลายแถว และแต่ละแถวเลือกระดับได้ตามนี้

| เลือก | ความหมาย | ปีการศึกษา |
|---|---|---|
| แผนก | ดูแลทั้งแผนก | ทุกปี |
| แผนก + ชั้นปี | ทุกห้องของชั้นปีนั้นในแผนก | ทุกปี |
| แผนก + ชั้นปี + ห้อง | เฉพาะห้องนั้น | เฉพาะปีของห้อง ต้องกำหนดใหม่เมื่อขึ้นปีการศึกษาใหม่ |

ขอบเขตนี้กำหนดสิทธิ์สร้าง/แก้ไขกิจกรรมของครู ส่วนครูที่ปรึกษาที่ตั้งในหน้าห้องเรียนจะได้สิทธิ์ของห้องนั้นด้วย หน้ารายละเอียดของครูแสดงขอบเขตและจำนวนนักเรียนที่ดูแล และหน้ารายละเอียดของนักเรียนแสดงครูที่ดูแล

## นำเข้าครูและนักเรียนจาก Excel

ไปที่ **ผู้ใช้งาน → นำเข้าจาก Excel** แล้วทำตามนี้

1. เลือกประเภท (นักเรียนหรือครู) แล้วดาวน์โหลดแม่แบบ
2. กรอกข้อมูลที่แผ่น `นำเข้า` (แผ่น `ตัวอย่าง` และ `ข้อมูลอ้างอิง` ใช้ดูวิธีกรอกและค่าที่ใช้ได้)
3. อัปโหลดไฟล์เพื่อตรวจสอบ ขั้นนี้ยังไม่บันทึกข้อมูล
4. ถ้าไม่มีข้อผิดพลาด กดยืนยันเพื่อนำเข้า

- รองรับ `.xlsx` ขนาดไม่เกิน 5 MB และไม่เกิน 2,000 แถวต่อไฟล์
- นำเข้าแบบทั้งไฟล์: ถ้ามีแถวผิดจะไม่บันทึกเลย แก้ไฟล์แล้วอัปโหลดใหม่
- เพิ่มได้เฉพาะคนใหม่ อีเมลหรือรหัสที่มีในระบบแล้วถือเป็นข้อผิดพลาด ไม่เขียนทับข้อมูลเดิม
- ครูที่ดูแลหลายขอบเขต ให้กรอกหลายแถวด้วยอีเมลเดียวกัน
- ถ้าไม่กรอกรหัสผ่าน ระบบจะสุ่มให้และดาวน์โหลดไฟล์รหัสผ่านได้ทันทีหลังนำเข้า ไฟล์นี้ได้ครั้งเดียวเพราะระบบไม่เก็บรหัสที่อ่านได้ ควรเก็บอย่างปลอดภัยและลบแม่แบบที่มีรหัสผ่านหลังใช้งาน

## ดูโครงสร้างของแผนก

ที่หน้า **แผนก/สาขา** คลิกชื่อแผนกเพื่อดูจำนวนชั้นปีและห้อง รายชื่อนักเรียนปัจจุบันของแต่ละห้อง และครูผู้ดูแลพร้อมขอบเขต เลือกปีการศึกษาได้ ส่วนรายชื่อย้อนหลังดูจากประวัติการศึกษาของนักเรียนแต่ละคน

## การเลื่อนชั้น

หน้า **เลื่อนชั้นนักเรียน** ทำงาน 3 ขั้น: เลือกปีและกติกา → กำหนดรายคน → สรุปก่อนยืนยัน

- กติกาเริ่มต้นตั้งที่ **โครงสร้างการศึกษา → ชั้นปี** (ชั้นถัดไป และชั้นปีสุดท้าย) ปรับเฉพาะรอบได้ที่หน้าเลื่อนชั้น
- ต้องมีห้องเรียนในปีปลายทางก่อน กดปุ่ม **สร้างห้องปีปลายทางจากปีต้นทาง** เพื่อคัดลอกโครงห้องได้
- การยืนยันทำใน Transaction บันทึกลง `PromotionHistories`, `PromotionHistoryDetails` และเพิ่ม `StudentAcademicHistories` โดยไม่แก้ไขประวัติเดิม
- นักเรียนที่เลื่อนแล้วจะไม่อยู่ในรายชื่อปีต้นทาง จึงดำเนินการซ้ำไม่ได้ ส่วนนักเรียนที่พักการเรียนยังคงอยู่ห้องเดิมและดำเนินการต่อในรอบถัดไปได้

## โครงสร้างโปรเจกต์

```text
Areas/Admin/         ข้อมูลหลัก: สถานที่ ประเภทกิจกรรม สถานะนักเรียน
Controllers/         MVC Controller และ AJAX Action
Data/                DbContext, Seed, Migration
Infrastructure/      เวลา, ผลลัพธ์ Service, ตัวกรอง error, การแสดงผลภาษาไทย
Models/Entities/     Entity ของฐานข้อมูล
Models/ViewModels/   ViewModel/DTO สำหรับ View และ JSON
Repositories/        การเข้าถึงข้อมูล
Services/            กฎธุรกิจ สิทธิ์ เลื่อนชั้น รายงาน
Views/               Razor View และ Partial
wwwroot/             CSS, jQuery ของแต่ละหน้า และไลบรารีในเครื่อง (lib/)
```

Bootstrap, jQuery, FullCalendar และไอคอนอยู่ใน `wwwroot/lib` จึงใช้งานในเครือข่ายภายในได้ มีเพียงฟอนต์ Noto Sans Thai ที่โหลดจาก Google Fonts หากออฟไลน์จะใช้ฟอนต์สำรองของเครื่อง

## ปัญหาที่พบบ่อย

| อาการ | วิธีแก้ |
|---|---|
| `A compatible .NET SDK was not found` | ติดตั้ง .NET 10 SDK |
| `Login failed for user 'sa'` | ตรวจรหัสผ่านใน Connection String |
| ข้ามการ Seed ใน log | ฐานข้อมูลยังเชื่อมต่อไม่ได้ หรือยังไม่ได้รัน `dotnet ef database update` |
| เข้าสู่ระบบ Admin ไม่ได้ในโหมด Production | กำหนด `Seed:AdminPassword` แล้วเริ่มระบบใหม่ |
