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

/** Drops trailing slashes so `${base}${path}` never produces `//`. */
export function trimTrailingSlashes(url) {
  let end = url.length;
  while (end > 0 && url[end - 1] === '/') end--;
  return url.slice(0, end);
}

/**
 * Builds a weighted picker over named request shapes.
 *
 * `weights` is the raw `-e WEIGHTS="name:n,…"` string: named shapes take the given weight, the rest
 * keep their default, and 0 removes a shape. `scenario` pins one shape and makes weights irrelevant.
 * Unknown names are rejected rather than ignored — a typo that silently drops to the defaults would
 * report the wrong mix as if it were the one asked for.
 *
 * Randomness is mulberry32 seeded from `seed`, not Math.random: this chooses which query to send and
 * protects nothing, so cryptographic strength is beside the point, while being able to replay a run
 * shape-for-shape with the same SEED is worth a lot when comparing two builds.
 */
export function buildShapePicker(cases, { weights = '', scenario = '', seed = '' } = {}) {
  const known = () => cases.map((c) => c.name).join(', ');

  const overrides = weights
    .split(',')
    .map((pair) => pair.trim())
    .filter(Boolean)
    .reduce((acc, pair) => {
      const [name, value] = pair.split(':').map((part) => part.trim());
      const weight = Number(value);
      if (!name || !Number.isFinite(weight) || weight < 0) {
        throw new Error(`Bad WEIGHTS entry "${pair}" — expected name:number`);
      }
      if (!cases.some((c) => c.name === name)) {
        throw new Error(`Unknown shape "${name}" in WEIGHTS. Known: ${known()}`);
      }
      acc[name] = weight;
      return acc;
    }, {});

  const pinned = scenario.trim();
  if (pinned && !cases.some((c) => c.name === pinned)) {
    throw new Error(`Unknown SCENARIO "${pinned}". Known: ${known()}`);
  }

  const active = (pinned ? cases.filter((c) => c.name === pinned) : cases)
    .map((c) => ({ ...c, weight: pinned ? 1 : (overrides[c.name] ?? c.weight) }))
    .filter((c) => c.weight > 0);
  if (active.length === 0) throw new Error('Every shape has weight 0 — nothing to run.');

  const cumulative = [];
  let running = 0;
  for (const c of active) {
    running += c.weight;
    cumulative.push(running);
  }

  let state = (Number.parseInt(seed, 10) || 0x9e3779b9) >>> 0;
  const nextRandom = () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };

  return function pickShape() {
    const roll = nextRandom() * running;
    for (let i = 0; i < cumulative.length; i++) {
      if (roll < cumulative[i]) return active[i];
    }
    return active.at(-1); // floating-point guard
  };
}
