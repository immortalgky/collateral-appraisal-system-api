# Self-Host Roboto Font (Remove Google Fonts Dependency)

## Todo
- [x] Download Roboto woff2 font files to `Assets/fonts/`
- [x] Add `@font-face` declarations to `index.css`
- [x] Remove Google Fonts `<link>` tag from `Login.cshtml`
- [x] Verify `.csproj` includes font files (already covered by `Assets\**\*`)
- [x] Build succeeds with no errors
- [x] Font files present in build output

## Review

### Changes Made

**1. `Modules/Auth/OAuth2OpenId/Assets/fonts/` (NEW)**
- Added `Roboto-latin.woff2` (37KB) — covers U+0000-00FF (standard ASCII/Latin)
- Added `Roboto-latin-ext.woff2` (24KB) — covers extended Latin characters

Key discovery: Roboto v51 is now a **variable font** — one woff2 file contains all weight variations (300-600). This means we only need 2 files (latin + latin-ext subsets) instead of 4 separate weight files.

**2. `Modules/Auth/OAuth2OpenId/Assets/index.css`**
- Added two `@font-face` declarations at the top with `font-weight: 100 900` (variable font range)
- Kept `unicode-range` from Google's own CSS to enable proper subset loading

**3. `Modules/Auth/OAuth2OpenId/Pages/Account/Login.cshtml`**
- Removed: `<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Roboto:wght@300;400;500;600&display=swap"/>`

**4. `Modules/Auth/OAuth2OpenId/OAuth2OpenId.csproj`**
- No changes needed — existing `<Content Include="Assets\**\*">` already covers the `fonts/` subdirectory
