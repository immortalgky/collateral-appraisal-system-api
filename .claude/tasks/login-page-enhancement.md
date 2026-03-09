# Login Page Enhancement

## Todo
- [x] Add `RememberMe` bind property to Login.cshtml.cs, pass to `PasswordSignInAsync`
- [x] Add error message display in Login.cshtml
- [x] Wire Remember Me checkbox with `asp-for="RememberMe"`
- [x] Add loading state script to disable button on submit
- [x] Add error message, loading spinner, and button disabled styles to index.css
- [x] Update font import for weight variety
- [x] `dotnet build` to verify — 0 errors

## Review

### Changes Summary

**3 files modified:**

1. **Login.cshtml.cs** — Added `[BindProperty] public bool RememberMe { get; set; }` and changed `PasswordSignInAsync` from hardcoded `true` to `RememberMe`.
2. **Login.cshtml** — Added error message div, wired checkbox with `asp-for="RememberMe"`, added loading script, updated font import weights.
3. **index.css** — Added `.error-message` alert style, `.login-button:disabled` + `.login-button.loading` with CSS spinner, hover state, letter-spacing on heading.

### Security Notes
- No sensitive data exposed in the frontend
- Anti-forgery token still in place
- `RememberMe` defaults to `false` (unchecked) — safer default than the previous hardcoded `true`
- No inline event handlers; script uses addEventListener
