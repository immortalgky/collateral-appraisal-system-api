// Shared k6 wiring for the read-side load tests (search-tasks.js, search-appraisals.js).
//
// Both scripts drive a paginated list endpoint the same way — same two load shapes, same bearer
// auth, same threshold skeleton — so that part lives here rather than being copied per script.
// Anything specific to an endpoint (its query shapes, its checks) stays in the script.

/**
 * Builds the k6 `scenarios` block.
 *
 * "count" runs an exact number of requests at a fixed concurrency — use it to compare two
 * builds, because the work done is identical across runs. "rate" ramps the arrival rate to
 * find the point where latency stops tracking throughput.
 */
export function buildScenarios(cfg) {
  const {
    mode = 'count',
    vus = 10,
    iterations = 300,
    peakRps = 20,
    preAllocatedVUs = 20,
    maxVUs = 200,
    warmup = '1m',
    stageDuration = '3m',
    name = 'search',
  } = cfg ?? {};

  if (mode !== 'rate') {
    return {
      [`${name}_count`]: {
        executor: 'shared-iterations',
        vus,
        iterations,
        maxDuration: '1h',
      },
    };
  }

  return {
    [`${name}_rate`]: {
      executor: 'ramping-arrival-rate',
      startRate: Math.max(1, Math.round(peakRps * 0.25)),
      timeUnit: '1s',
      preAllocatedVUs,
      maxVUs,
      stages: [
        { target: Math.max(1, Math.round(peakRps * 0.5)), duration: warmup },
        { target: peakRps, duration: stageDuration },
        { target: peakRps * 2, duration: stageDuration },
        { target: peakRps * 4, duration: stageDuration },
        { target: 0, duration: '30s' },
      ],
    },
  };
}

/**
 * Request headers for a bearer token, accepting it with or without the "Bearer " prefix.
 * Throws when the token is missing: every one of these endpoints scopes rows by the caller,
 * so an unauthenticated run would measure an empty result set and look misleadingly fast.
 */
export function authHeaders(token, why) {
  if (!token) {
    throw new Error(`TOKEN is required: pass -e TOKEN="<jwt>". ${why ?? ''}`.trim());
  }
  return {
    Accept: 'application/json',
    Authorization: token.toLowerCase().startsWith('bearer ') ? token : `Bearer ${token}`,
  };
}

/** The thresholds both scripts assert: no errors, checks passing, and a p95 budget. */
export function listThresholds(tagName, p95Ms) {
  return {
    http_req_failed: ['rate<0.01'],
    [`http_req_duration{name:${tagName}}`]: [`p(95)<${p95Ms}`],
    checks: ['rate>0.99'],
  };
}
