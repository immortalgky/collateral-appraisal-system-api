#!/usr/bin/env bash
#
# Signs a bank (internal) user in the way the SPA does and prints the session cookie, for use as k6's
# -e SESSION_COOKIE=... (send it as the `Cookie` header; unsafe requests also need an Origin header that is
# the SPA origin or the API origin).
#
# The appraisal list is company-scoped: AppraisalAccessScope forces a company filter for any
# caller carrying a company_id claim. `X-Dev-Auth: dev-bypass` stamps no company_id on purpose,
# so it matches no rows and is useless for load testing — hence this real login.
#
# The SPA holds no tokens any more: the API is the OIDC client and keeps a server-side session behind an
# HttpOnly cookie. The script walks the same interactive flow a browser does:
#   /auth/login -> /connect/authorize -> /Account/Login (antiforgery) -> /connect/authorize -> /signin-oidc
# and prints "<cookie name>=<value>" read from the cookie jar.
#
# Usage:  export SESSION_COOKIE=$(docs/load-test/get-appraisal-token.sh admin '<dev password>' [base_url])
#   base_url must be the API's AppBaseUrl exactly (same host and port). On a mismatch /auth/login only logs
#   [AUTH-HOST-MISMATCH]; the sign-in then fails later at /signin-oidc with a state/correlation error.
#
set -euo pipefail
USER="${1:?usage: get-appraisal-token.sh <username> <password> [base_url]}"
PASS="${2:?usage: get-appraisal-token.sh <username> <password> [base_url]}"
BASE="${3:-https://localhost:7111}"
SPA_RETURN="${SPA_RETURN:-https://localhost:3000/}"   # any origin in Cors:AllowedOrigins

JAR=$(mktemp); trap 'rm -f "$JAR"' EXIT
curl_() { curl -sk -c "$JAR" -b "$JAR" "$@"; }
location_() { awk 'tolower($1)=="location:"{print $2}' | tr -d '\r' | tail -1; }
absolute_() { case "$1" in /*) printf '%s%s' "$BASE" "$1";; *) printf '%s' "$1";; esac; }

# 1. Start sign-in: /auth/login redirects to /connect/authorize, which (prompt=login) redirects to the login page.
AUTHZ=$(curl_ -o /dev/null -D - "$BASE/auth/login?returnUrl=$SPA_RETURN" | location_)
[[ -n "$AUTHZ" ]] || { echo "/auth/login did not redirect (host mismatch with AppBaseUrl?)" >&2; exit 1; }
LOGIN_LOC=$(curl_ -o /dev/null -D - "$(absolute_ "$AUTHZ")" | location_)
[[ -n "$LOGIN_LOC" ]] || { echo "authorize did not redirect to the login page" >&2; exit 1; }
LOGIN_URL=$(absolute_ "$LOGIN_LOC")
RETURN_URL_ENC=$(printf '%s' "$LOGIN_URL" | sed -n 's/.*ReturnUrl=\([^&]*\).*/\1/p')
RETURN_URL=$(printf '%s' "$RETURN_URL_ENC" | python3 -c 'import sys,urllib.parse;print(urllib.parse.unquote(sys.stdin.read()))')

# 2. The login form (antiforgery token), then submit it. A browser sends Origin on a same-origin form POST.
AF=$(curl_ "$LOGIN_URL" \
     | sed -n 's/.*name="__RequestVerificationToken"[^>]*value="\([^"]*\)".*/\1/p' | head -1)
[[ -n "$AF" ]] || { echo "could not read the antiforgery token from the login page" >&2; exit 1; }

CB=$(curl_ -o /dev/null -D - -X POST "$LOGIN_URL" -H "Origin: $BASE" \
      --data-urlencode "Username=$USER" \
      --data-urlencode "Password=$PASS" \
      --data-urlencode "ReturnUrl=$RETURN_URL" \
      --data-urlencode "__RequestVerificationToken=$AF" \
     | location_)
[[ -n "$CB" ]] || { echo "login failed — check the username/password" >&2; exit 1; }

# 3. authorize -> /signin-oidc?code=...: the API redeems the code and sets the session cookie.
CODE_LOC=$(curl_ -o /dev/null -D - "$(absolute_ "$CB")" | location_)
[[ "$CODE_LOC" == *"/signin-oidc"* ]] || { echo "no authorization callback returned; got: $CODE_LOC" >&2; exit 1; }
curl_ -o /dev/null "$CODE_LOC"

# Netscape jar: HttpOnly cookies are written with a "#HttpOnly_" host prefix, so don't skip comment lines.
COOKIE=$(awk -F'\t' '$6 ~ /^__Host-cas-session/ {print $6 "=" $7}' "$JAR" | tail -1)
[[ -n "$COOKIE" ]] || { echo "sign-in did not set the session cookie" >&2; exit 1; }
printf '%s\n' "$COOKIE"
