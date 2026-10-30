# Release Process Guidebook — SIT / UAT / PRD

> **สถานะ:** ใช้งาน ตั้งแต่ 2026-10-30 (รอบแรก `v6.0`) — งานที่ยังค้างดู §8
> **ใช้กับ:** `collateral-appraisal-system-api` (BE) และ `collateral-appraisal-system-app` (FE) — สอง repo ใช้กติกาเดียวกัน เลขเดียวกัน
> **รอบ PRD:** ทุก 2 สัปดาห์

---

## 1. หลักการ 5 ข้อ (จำแค่นี้พอ)

1. **`main` = trunk** — ทุกคน merge งานเข้า `main` ผ่าน PR เท่านั้น → deploy SIT
2. **ไม่มี branch ถาวรต่อ environment** — ไม่มี `sit` / `uat` / `prd` branch
3. **ตัด `release/vX.Y` ทุกรอบ** — UAT และ PRD มาจาก branch นี้เท่านั้น
4. **Build ครั้งเดียว deploy หลายที่** — PRD ได้ zip ตัวเดียวกับที่ UAT sign-off ห้าม build ใหม่
5. **ทุก fix บน release branch → cherry-pick กลับ `main` วันเดียวกัน** — ไม่ merge release branch กลับ `main` ทั้งก้อน

---

## 2. Branch และ Tag

| ชื่อ | ตัวอย่าง | ตัดจาก | ใช้ทำอะไร | อายุ |
|---|---|---|---|---|
| `main` | — | — | trunk, SIT | ถาวร |
| `feat/<เรื่อง>` / `fix/<ticket>-<เรื่อง>` | `feat/pricing-land-role` | `main` | งานปกติ | จน merge |
| `release/vX.Y` | `release/v6.1` | `main` | UAT + PRD ของรอบนั้น | จนรอบถัดไปขึ้น PRD |
| `fix/<ticket>-<เรื่อง>` (ไป release) | `fix/CA-401-fee-rounding` | `release/vX.Y` | defect UAT | จน merge |
| `hotfix/<ticket>-<เรื่อง>` | `hotfix/CA-450-login` | `release/vX.Y` | แก้ PRD ด่วน | จน merge |

| Tag | ความหมาย | Deploy ไป |
|---|---|---|
| `vX.Y.0-rcN` | **Release Candidate** ครั้งที่ N — ตัวที่ส่งให้ UAT ทดสอบ | UAT |
| `vX.Y.0` | ตัวที่ UAT sign-off แล้ว (commit เดียวกับ rc สุดท้าย) | PRD |
| `vX.Y.Z` (Z ≥ 1) | hotfix ของ PRD | PRD |

**เลขเวอร์ชัน** (`vMAJOR.MINOR.PATCH` ตาม [semver](https://semver.org)):
- รอบปกติ 2 สัปดาห์ → เพิ่ม **MINOR** (`v6.1` → `v6.2`)
- hotfix → เพิ่ม **PATCH** (`v6.1.0` → `v6.1.1`)
- เปลี่ยนใหญ่ / เปลี่ยนวิธี deploy → เพิ่ม **MAJOR**
- เริ่มใช้ระบบนี้ที่ **`v6.0.0`** เพื่อไม่ปนกับ tag เดิม `uat-v5.0.0-r2`

**ทั้ง BE และ FE ตัด branch และ tag เลขเดียวกันวันเดียวกันเสมอ** แม้ repo ไหนจะไม่มีอะไรเปลี่ยน — จะได้รู้ทันทีว่า BE ตัวไหนคู่กับ FE ตัวไหน

---

## 3. ปฏิทิน 1 รอบ (2 สัปดาห์)

```
สัปดาห์       1                      2                      3
main     ──●──●──●──●──●──●──●──●──●──●──●──●──●──●──●──►   (งาน v6.2 เข้า SIT ต่อได้เลย)
             \                                  \
        release/v6.1 ─●─────●──────●──         release/v6.2 ─●── ...
               rc1    rc2    rc3 = v6.1.0 → PRD      rc1
                                  \
                                   ● v6.1.1 (hotfix ถ้ามี)
```

| วัน | เหตุการณ์ |
|---|---|
| จันทร์ สัปดาห์ 1 | ตัด `release/v6.1` (BE + FE) → `v6.1.0-rc1` → UAT |
| สัปดาห์ 1–2 | UAT ทดสอบ, แก้ defect → `rc2`, `rc3`, … |
| ปลายสัปดาห์ 2 | UAT sign-off → tag `v6.1.0` → PRD |
| วันเดียวกัน / จันทร์ถัดไป | ตัด `release/v6.2` → รอบใหม่ |

ระหว่าง `release/v6.1` อยู่ใน UAT, `main` เดินต่อสำหรับ v6.2 ได้ตามปกติ

---

## 4. Scenario

> ทุกคำสั่งทำใน **worktree** (`git worktree add ../<dir> origin/<branch>`) ไม่ใช่ checkout หลัก
> ทุกครั้งที่ทำ: **ทำ BE และ FE คู่กัน** ถ้าเรื่องนั้นแตะทั้งสอง repo

### 4.1 งานปกติ (dev → SIT)

```bash
git fetch
git switch -c feat/xxx origin/main
# ... เขียนโค้ด ...
gh pr create --base main            # รอ dotnet-ci ผ่าน → merge
```

- `main` ต้อง **พร้อมขึ้นได้เสมอ** — งานที่ยังไม่เสร็จ **อย่า merge** ค้างไว้ใน PR
- งานใหญ่เกิน 2 สัปดาห์ → ซ่อนหลัง config flag แล้ว merge ทีละชิ้น
- SIT deploy จาก `main` ล่าสุด

### 4.2 เตรียม UAT — ตัด release

ทำใน **ทั้งสอง repo**:

```bash
git fetch
git switch -c release/v6.1 origin/main
git push -u origin release/v6.1

git tag -a v6.1.0-rc1 -m "v6.1.0-rc1 UAT"
git push origin v6.1.0-rc1
```

Build และ deploy:

```powershell
# checkout tag ทั้ง BE และ FE ให้ตรงกันก่อน
pwsh deploy/Publish.ps1 -Version v6.1.0-rc1
# → ได้ zip → DB program → API → Web (ตาม deploy/README.md)
```

- **เก็บ zip ของทุก rc ไว้** — ตัวที่ผ่าน UAT คือตัวที่จะขึ้น PRD
- ตั้งแต่ตัดแล้ว `release/v6.1` รับ **bug fix เท่านั้น** feature ใหม่รอรอบหน้า
- แจ้ง UAT: เวอร์ชัน, รายการ ticket ที่อยู่ในรอบนี้ (`git log --oneline v6.0.0..v6.1.0-rc1`)

### 4.3 แก้ defect ระหว่าง UAT

```bash
# 1) แก้บน release
git switch -c fix/CA-401-xxx origin/release/v6.1
# ... แก้ ...
gh pr create --base release/v6.1          # squash merge (ได้ commit เดียว cherry-pick ง่าย)

# 2) ส่งกลับ main — วันเดียวกัน
git fetch
git switch -c fix/CA-401-xxx-main origin/main
git cherry-pick -x <squash-commit-sha>     # -x ใส่ "(cherry picked from commit …)" ใน message
gh pr create --base main

# 3) แก้ครบชุดที่จะส่ง → ออก rc ใหม่
git fetch
git tag -a v6.1.0-rc2 origin/release/v6.1 -m "v6.1.0-rc2 UAT"
git push origin v6.1.0-rc2
# → Publish.ps1 -Version v6.1.0-rc2 → deploy UAT
```

**ถ้า bug นั้นไม่มีใน `main` แล้ว** (โค้ดถูกเขียนใหม่ไปแล้ว) — ยังเปิด PR ไป main ได้ หรือเขียนในคำอธิบาย PR ของ release ว่า "ไม่ต้อง port: main แก้แล้วใน #xxx"

**ถ้า UAT ขอ feature ที่ merge เข้า `main` ไปแล้วให้เข้ารอบนี้ด้วย** — cherry-pick จาก main เข้า release ได้ แต่ต้องตกลงกับ UAT ก่อน และถือเป็นข้อยกเว้น ไม่ใช่เรื่องปกติ

### 4.4 เตรียม PRD

**ก่อนขึ้น — เช็คว่าไม่มี fix ไหนลืมส่งกลับ main:**

```bash
git fetch
git cherry -v origin/main origin/release/v6.1
# "+" = ยังไม่อยู่ใน main → ไปเปิด PR port
# "-" = อยู่ใน main แล้ว
```

> ถ้าตอน cherry-pick ต้องแก้ conflict เอง `git cherry` อาจขึ้น "+" ทั้งที่ port แล้ว
> ให้ยืนยันด้วย: `git log origin/main --grep "cherry picked from commit <sha>"`

**Tag และ deploy:**

```bash
# tag v6.1.0 ชี้ commit เดียวกับ rc ที่ UAT ผ่าน (^{commit} = ชี้ commit ไม่ใช่ชี้ tag)
git tag -a v6.1.0 "v6.1.0-rc3^{commit}" -m "v6.1.0 PRD"
git push origin v6.1.0
```

- เอา **zip ของ `v6.1.0-rc3` ตัวเดิม** ขึ้น PRD — **ห้าม build ใหม่**
- ลำดับ: DB program → node 1 (API, Web, health) → node 2 (API, Web, health)
- สร้าง GitHub Release จาก tag `v6.1.0` ทั้งสอง repo ใส่ release notes
- ตัด `release/v6.2` ได้เลย (กลับไป 4.2)

### 4.5 Hotfix PRD

```bash
# ตัดจาก release ของเวอร์ชันที่อยู่บน PRD
git switch -c hotfix/CA-450-xxx origin/release/v6.1
# ... แก้ ...
gh pr create --base release/v6.1          # squash merge

git fetch
git tag -a v6.1.1-rc1 origin/release/v6.1 -m "v6.1.1-rc1"
git push origin v6.1.1-rc1
# → build → ทดสอบ → ผ่าน → git tag -a v6.1.1 "v6.1.1-rc1^{commit}" → zip เดิมขึ้น PRD
```

**ส่งกลับ 2 ที่ (ห้ามลืม):**

| ส่งไป | ถ้าไม่ส่ง |
|---|---|
| `main` | bug กลับมาในรอบถัด ๆ ไป |
| `release/v6.2` (ถ้าตัดไปแล้ว) | v6.2 ขึ้น PRD → bug กลับมาทันที |

**⚠ ระวังเรื่องทดสอบ hotfix บน UAT:**
แอปตอน start เช็คแค่ว่า DB **ตามหลัง** build หรือไม่ (`Shared/Shared/Data/Extensions/MigrationExtension.cs`)
ถ้า UAT มี DB ของ v6.2 อยู่แล้ว (DB **ล้ำหน้า**) แอป v6.1.1 จะ start ได้ปกติ **โดยไม่เตือน** — แต่ไม่ใช่สภาพเดียวกับ PRD

- hotfix **แตะแค่โค้ด** → ทดสอบบน UAT ได้ แล้ว deploy rc ของ v6.2 กลับคืนหลังทดสอบเสร็จ
- hotfix **มี migration** → พยายามเลี่ยง; ถ้าจำเป็นต้องเป็นแบบ **เพิ่มอย่างเดียว + idempotent**
  - ตอน cherry-pick เข้า `main` จะ conflict ที่ `*ModelSnapshot.cs` → แก้มือ
  - **คงชื่อ migration เดิม** (timestamp เดิม) เพื่อให้ history ใน PRD ตรงกัน

### 4.6 Rollback PRD

- App: `Deploy-App.ps1` / `Deploy-Web.ps1` เก็บ backup ไว้ที่ `C:\Deploy\backups\<ver>\` → copy กลับ
- DB: ใช้ `rollback/` ใน zip (รันมือ) — ดู `deploy/README.md`
- ถ้า migration รอบนั้นลบ/เปลี่ยน column → rollback app อย่างเดียวอาจพัง ให้ hotfix ไปข้างหน้าแทน

### 4.7 หลังรอบถัดไปขึ้น PRD

- `release/v6.1` ไม่ต้องใช้แล้ว (ไม่มีใครจะ hotfix v6.1 อีก) → ลบ branch ได้ **tag ยังอยู่ครบ**
- ลบ zip ของ rc ที่ไม่ได้ขึ้น PRD ได้ เก็บเฉพาะ zip ที่ขึ้น PRD

---

## 5. Frontend: build ครั้งเดียวใช้ทุก env

### ปัญหาตอนนี้

Vite ฝังค่า `VITE_*` ลงใน JS **ตอน build** → ต้อง build แยกทีละ env (`.env.sit1–4`, `.env.uat1–4`, `.env.production`)
และ BE bundle ก็ฝัง `$ApiBaseUrl$` ลงใน SQL ตอน publish (อ่านจาก `.env.<mode>` ของ FE)

### แนวทาง (ยังไม่ได้ทำ — ดู §8)

ย้ายค่าที่ต่างกันต่อ env ไปไว้ใน **`config.js` บน server** อ่านตอนรัน — แบบเดียวกับ `appsettings.Production.json` ของ BE

```js
// <IIS web root>\config.js — ต่างกันต่อ env, ไม่อยู่ใน zip
window.__CAS_CONFIG__ = {
  apiUrl: 'https://<api-host>/api',
  appUrl: 'https://<app-host>',
  googleMapsApiKey: '<browser key>',
};
```

- `index.html` โหลด `<script src="/config.js"></script>` ก่อนแอป
- `src/config/index.ts` อ่าน `window.__CAS_CONFIG__` ก่อน ไม่มีค่อย fallback `.env` (dev ยังใช้ `.env.local` ได้)
- `Deploy-Web.ps1` ต้อง `/XF config.js` (ไม่งั้น `/MIR` ลบทิ้ง)
- `web.config` ตั้ง `config.js` เป็น no-cache
- BE: คง token `$ApiBaseUrl$` ใน bundle แล้วแทนค่าตอน deploy จาก `C:\Deploy\config\<env>.ps1`

### ความปลอดภัย

`config.js` กับ `.env` **ปลอดภัยเท่ากัน** — ค่าทุกตัวที่ FE ใช้ ผู้ใช้เปิด DevTools ก็เห็นได้ทั้งคู่
(ตอนนี้ Maps key ก็อยู่ใน `dist/assets/*.js` อยู่แล้ว)

- **ห้ามใส่ secret ใน FE ทุกรูปแบบ** — client secret, password, key ของระบบภายใน → ให้ BE เรียกแทน
- Google Maps key ป้องกันด้วย **HTTP referrer restriction + API restriction + quota** ใน Google Cloud Console ไม่ใช่การซ่อน

### ลำดับ deploy และความเข้ากันได้

- ลำดับ: **DB → API → Web** (ต่อ node)
- ระหว่าง node 1 อัปแล้ว node 2 ยังไม่อัป จะมีช่วงที่ FE/BE ต่างเวอร์ชันกัน → **BE ต้อง backward-compatible กับ FE รุ่นก่อนเสมอ** (เพิ่ม field ได้, ห้ามลบ/เปลี่ยนชื่อ field ที่ FE เก่ายังใช้ในรอบเดียวกัน)

---

## 6. Checklist

### ตัด release (ทุก 2 สัปดาห์)
- [ ] `main` ของ BE และ FE CI เขียว
- [ ] ตัด `release/vX.Y` ทั้งสอง repo
- [ ] tag `vX.Y.0-rc1` ทั้งสอง repo
- [ ] `Publish.ps1 -Version vX.Y.0-rc1` → เก็บ zip
- [ ] deploy UAT → health ผ่าน
- [ ] แจ้ง UAT พร้อมรายการ ticket

### ออก rc ใหม่
- [ ] PR fix merge เข้า `release/vX.Y` แล้ว
- [ ] PR port ไป `main` เปิดแล้ว (หรือระบุว่าไม่ต้อง port)
- [ ] tag `vX.Y.0-rcN` → build → deploy UAT

### ขึ้น PRD
- [ ] UAT sign-off ระบุเลข rc ชัดเจน
- [ ] `git cherry -v origin/main origin/release/vX.Y` ไม่มี "+" ที่ยังไม่ port
- [ ] tag `vX.Y.0` บน commit ของ rc นั้น ทั้งสอง repo
- [ ] ใช้ **zip เดิมของ rc นั้น**
- [ ] DB program → node 1 → node 2 → health ผ่าน
- [ ] GitHub Release + release notes
- [ ] ตัด `release/vX.(Y+1)`

### Hotfix
- [ ] ตัดจาก `release/vX.Y` ของเวอร์ชันบน PRD
- [ ] ไม่มี migration (หรือ additive + idempotent)
- [ ] tag `vX.Y.Z-rc1` → ทดสอบ → `vX.Y.Z` → PRD
- [ ] port ไป `main`
- [ ] port ไป `release/vX.(Y+1)` ถ้าตัดไปแล้ว

---

## 7. FAQ

**rc คืออะไร?** — *Release Candidate* = build ที่คิดว่าพร้อมขึ้น PRD แต่รอ UAT ยืนยัน ตัวที่ผ่านจะได้ tag ไม่มี `-rc`

**merge release กลับ main ตอนไหน?** — ไม่ merge ทั้งก้อน ทุก fix cherry-pick กลับทันทีวันที่ merge เข้า release

**ทำไมไม่ให้ `main` = PRD (GitFlow)?** — ต้องมี `develop` + merge สองทอด + back-merge → conflict และ drift เยอะ ส่วน "อะไรอยู่บน PRD" ดูจาก tag ได้อยู่แล้ว (`git log v6.0.0..v6.1.0`)

**แก้ใน release ตรง ๆ ไม่ผ่าน PR ได้ไหม?** — ไม่ได้ (เมื่อตั้ง ruleset แล้ว) เพราะ PRD ออกจาก branch นี้

**งานยังไม่เสร็จแต่ถึงวันตัดแล้ว?** — ไม่ merge เข้า `main` = ไม่อยู่ในรอบนี้ ไปรอบหน้า

**ทำไม PRD ห้าม build ใหม่ทั้งที่ commit เดียวกัน?** — dependency ที่ resolve ตอน build, เครื่องที่ build, config ที่ฝัง อาจต่างกันได้ ตัวที่ UAT ทดสอบคือ *binary* ไม่ใช่ *commit*

---

## 8. สิ่งที่ต้องทำก่อนใช้จริง

| # | งาน | สถานะ |
|---|---|---|
| 1 | ตกลงใช้ guidebook นี้ + เริ่มรอบแรก `v6.0.0` | ✅ 2026-10-30 — ใช้ cherry-pick (ไม่ merge กลับ) |
| 2 | Ruleset ทั้งสอง repo (ดู §9) + กฎ agent ใน `CLAUDE.md` ทั้งสอง repo | ✅ 2026-10-30 |
| 3 | FE runtime config (`config.js`) + แก้ 13 ไฟล์ที่อ่าน `VITE_API_URL` เอง | ⏳ |
| 4 | `Deploy-Web.ps1` `/XF config.js` + `web.config` no-cache | ⏳ |
| 5 | BE: `$ApiBaseUrl$` แทนค่าตอน deploy แทนตอน publish | ⏳ |
| 6 | `Publish.ps1` เลิกใช้ `-FrontendMode`; ลบ `.env.sit*`, `.env.uat*`, `.env.production` | ⏳ หลังข้อ 3–5 |
| 7 | ตรวจ Google Maps key: referrer + API restriction + quota | ⏳ |
| 8 | เก็บกวาด branch เก่า (`fix/uat/*`, `hotfix/uat-fix`, `cherry-pick-defect-*`, `merge/uat-*`) | ⏳ |

ระหว่างข้อ 3–6 ยังไม่เสร็จ: build FE แยกต่อ env **จาก tag เดียวกัน** ไปก่อน (ยอมรับว่า PRD ไม่ได้ binary ตัวเดียวกับ UAT สำหรับ FE)

---

## 9. GitHub rulesets (ตั้งแล้ว 2026-10-30)

| Ruleset | ครอบ | กฎ | ใครข้ามได้ |
|---|---|---|---|
| Protect release branches | `release/**` | บังคับ PR (approve 0), **squash เท่านั้น**, ห้าม force-push, Copilot review, BE: `dotnet-ci` ต้องผ่าน (FE ไม่บังคับ CI) | ไม่มีใคร |
| Release branch deletion | `release/**` | ห้ามลบ | Repository admin |
| Protect release tags | `v*` | ห้ามลบ, ห้ามย้าย (สร้างใหม่ได้) | Repository admin |

- แยก "deletion" ออกมาเป็น ruleset ของตัวเอง เพราะ bypass ของ GitHub ใช้ทั้ง ruleset — ถ้ารวมกัน admin ที่ได้สิทธิ์ลบจะข้ามกฎ PR ไปด้วย
- ruleset `main` เดิมไม่ได้แตะ
- ดู/แก้: `gh api repos/immortalgky/<repo>/rulesets` หรือ GitHub → Settings → Rules → Rulesets
