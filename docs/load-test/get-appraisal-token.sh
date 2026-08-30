#!/usr/bin/env bash
#
# Prints an access token for a bank (internal) user, for use as k6's -e TOKEN=...
#
# The appraisal list is company-scoped: AppraisalAccessScope forces a company filter for any
# caller carrying a company_id claim. `X-Dev-Auth: dev-bypass` stamps company_id = Guid.Empty,
# so it matches no rows and is useless for load testing — hence this real login.
#
# The SPA client is authorization-code + PKCE only (the /connect/token controller rejects the
# password grant even though OpenIddict enables it), so this walks the full interactive flow:
#   /connect/authorize -> /Account/Login (antiforgery) -> /connect/authorize -> /auth/token
#
# Usage:  export TOKEN=$(docs/load-test/get-appraisal-token.sh admin '<dev password>')
#
set -euo pipefail
USER="${1:?usage: get-appraisal-token.sh <username> <password> [base_url]}"
PASS="${2:?usage: get-appraisal-token.sh <username> <password> [base_url]}"
BASE="${3:-https://localhost:7111}"
REDIRECT="$BASE/callback"

JAR=$(mktemp); trap 'rm -f "$JAR"' EXIT

# openssl rather than `tr </dev/urandom | head`: that pipeline dies of SIGPIPE under `set -o pipefail`.
VERIFIER=$(openssl rand -hex 32)
CHALLENGE=$(printf '%s' "$VERIFIER" | openssl dgst -binary -sha256 | openssl base64 | tr '+/' '-_' | tr -d '=')

# Scope must be exactly "openid profile" — the spa client has no scp:roles permission, and asking
# for roles or offline_access gets a 400 invalid_scope.
AUTHZ="$BASE/connect/authorize?client_id=spa&response_type=code&redirect_uri=$REDIRECT&scope=openid%20profile&code_challenge=$CHALLENGE&code_challenge_method=S256&state=k6"

LOGIN_LOC=$(curl -sk -c "$JAR" -b "$JAR" -o /dev/null -D - "$AUTHZ" \
            | awk 'tolower($1)=="location:"{print $2}' | tr -d '\r')
[ -n "$LOGIN_LOC" ] || { echo "authorize did not redirect to the login page" >&2; exit 1; }
case "$LOGIN_LOC" in /*) LOGIN_URL="$BASE$LOGIN_LOC";; *) LOGIN_URL="$LOGIN_LOC";; esac
RETURN_URL_ENC=$(printf '%s' "$LOGIN_URL" | sed -n 's/.*ReturnUrl=\([^&]*\).*/\1/p')
RETURN_URL=$(printf '%s' "$RETURN_URL_ENC" | python3 -c 'import sys,urllib.parse;print(urllib.parse.unquote(sys.stdin.read()))')

AF=$(curl -sk -c "$JAR" -b "$JAR" "$LOGIN_URL" \
     | sed -n 's/.*name="__RequestVerificationToken"[^>]*value="\([^"]*\)".*/\1/p' | head -1)
[ -n "$AF" ] || { echo "could not read the antiforgery token from the login page" >&2; exit 1; }

CB=$(curl -sk -c "$JAR" -b "$JAR" -o /dev/null -D - -X POST "$LOGIN_URL" \
      --data-urlencode "Username=$USER" \
      --data-urlencode "Password=$PASS" \
      --data-urlencode "ReturnUrl=$RETURN_URL" \
      --data-urlencode "__RequestVerificationToken=$AF" \
     | awk 'tolower($1)=="location:"{print $2}' | tr -d '\r' | tail -1)
[ -n "$CB" ] || { echo "login failed — check the username/password" >&2; exit 1; }
case "$CB" in /*) CB="$BASE$CB";; esac

CODE_LOC=$(curl -sk -c "$JAR" -b "$JAR" -o /dev/null -D - "$CB" \
           | awk 'tolower($1)=="location:"{print $2}' | tr -d '\r' | tail -1)
CODE=$(printf '%s' "$CODE_LOC" | sed -n 's/.*[?&]code=\([^&]*\).*/\1/p')
[ -n "$CODE" ] || { echo "no authorization code returned; got: $CODE_LOC" >&2; exit 1; }

curl -sk -X POST "$BASE/auth/token" -H "Content-Type: application/json" \
  -d "{\"grantType\":\"authorization_code\",\"clientId\":\"spa\",\"code\":\"$CODE\",\"codeVerifier\":\"$VERIFIER\",\"redirectUri\":\"$REDIRECT\"}" \
  | python3 -c 'import sys,json;print(json.load(sys.stdin)["accessToken"])'
