// Valid-code pools for the create-request load test.
//
// GEO: each entry is [subDistrictCode(6), postcode(5)] sampled from
//   Database/Migration/Scripts/20260311163100_SeedData_TitleSubDistricts.sql
// The Thai administrative geocode is prefix-hierarchical, so the parents are
// DERIVED from the subdistrict code — guaranteeing province/district/subdistrict
// are always synchronized:
//   province = code.slice(0, 2)   e.g. "10"
//   district = code.slice(0, 4)   e.g. "1001"
// (matches DistrictCode in the seed). ~200 entries spanning all 77 provinces.

export const GEO = [
  ["100101","10200"],["100704","10330"],["101803","10600"],["102803","10120"],["104002","10160"],
  ["110110","10280"],["110501","10290"],["120405","11110"],["130201","12120"],["130703","12160"],
  ["140208","13130"],["140423","13190"],["140703","13220"],["140907","13140"],["141302","13270"],
  ["150105","14000"],["150411","14120"],["160106","15000"],["160310","15120"],["160606","15110"],
  ["161005","15190"],["170501","16140"],["180401","17150"],["190111","18000"],["190402","18150"],
  ["190907","18120"],["200108","20000"],["200506","20160"],["200903","20180"],["210309","21110"],
  ["220106","22000"],["220501","22150"],["230104","23000"],["240103","24000"],["240402","24130"],
  ["240702","24120"],["250208","25110"],["250807","25140"],["260310","26110"],["270502","27160"],
  ["300104","30310"],["300304","30330"],["300803","30210"],["301014","30160"],["301405","30150"],
  ["301704","30270"],["302104","30130"],["302701","30270"],["310117","31000"],["310418","31110"],
  ["310801","31180"],["311106","31150"],["311701","31110"],["320107","32000"],["320404","32180"],
  ["320711","32130"],["321015","32150"],["321701","32130"],["330308","33130"],["330509","33140"],
  ["330808","33150"],["331101","33220"],["331704","33140"],["340120","34000"],["340504","34170"],
  ["341002","34230"],["341403","34140"],["341912","34110"],["342603","34160"],["350115","35000"],
  ["350602","35130"],["360110","36000"],["360502","36210"],["361005","36110"],["361504","36130"],
  ["370403","37180"],["380203","38150"],["380802","38000"],["390402","39180"],["400114","40000"],
  ["400511","40130"],["400911","40170"],["401405","40230"],["401805","40180"],["402903","40140"],
  ["410401","41110"],["410803","41290"],["411710","41160"],["412302","41130"],["420403","42150"],
  ["420910","42130"],["430116","43000"],["431501","43120"],["440306","44140"],["440610","44130"],
  ["440906","44120"],["450103","45000"],["450404","45180"],["450704","45110"],["451009","45120"],
  ["451403","45160"],["451904","45140"],["460402","46210"],["460803","46170"],["461302","46150"],
  ["470111","47000"],["470701","47270"],["471107","47170"],["471703","47230"],["480312","48120"],
  ["480712","48130"],["490106","49000"],["490601","49150"],["500305","50270"],["500701","50180"],
  ["501107","50190"],["501412","50210"],["501908","50140"],["510108","51000"],["510608","51120"],
  ["520307","52130"],["520806","52160"],["530105","53000"],["530602","53180"],["540112","54000"],
  ["540409","54130"],["550108","55000"],["550606","55140"],["551102","55210"],["560205","56150"],
  ["560703","56130"],["570305","57140"],["570705","57110"],["571203","57290"],["580206","58140"],
  ["600105","60000"],["600404","60110"],["600804","60160"],["601117","60150"],["610209","61120"],
  ["610613","61180"],["620304","62180"],["620706","62170"],["630302","63130"],["630806","63170"],
  ["640407","64170"],["640709","64110"],["650202","65120"],["650602","65150"],["660103","66000"],
  ["660413","66110"],["661002","66130"],["670209","67190"],["670504","67130"],["670807","67160"],
  ["700119","70000"],["700511","70110"],["700807","70140"],["710309","71220"],["710701","71180"],
  ["720102","72000"],["720305","72180"],["720707","72110"],["730103","73000"],["730215","73140"],
  ["730505","73130"],["740108","74000"],["750105","75000"],["760106","76000"],["760405","76120"],
  ["760703","76110"],["770406","77230"],["800119","80330"],["800612","80190"],["801002","80220"],
  ["801405","80120"],["802102","80160"],["810407","81120"],["820203","83000"],["830101","83000"],
  ["840210","84160"],["840807","84180"],["841403","84260"],["850101","85000"],["860108","86000"],
  ["860411","86110"],["900206","90190"],["900601","90210"],["901008","90320"],["901511","90330"],
  ["910604","91120"],["920305","92140"],["920703","92220"],["930302","93130"],["931001","93110"],
  ["940305","94170"],["940703","94110"],["941007","94160"],["950401","95150"],["960105","96000"],
  ["960608","96150"],
];

// Other dimensions that may vary (purpose / channel are FIXED in the payload builder
// per decision, so they are not pooled here).
export const BANKING_SEGMENTS = ["RETAIL", "IBG"];
export const PRIORITIES = ["Normal", "High"];

// Collateral type -> appraisal property family. The created AppraisalProperties.PropertyType
// is derived from the TITLE collateralType via AppraisalCreationService.CodeToAppraisalFamily:
//   "01" -> Land (L), "02" -> Land+Building (LB), "08" -> Condo (U).
// We set the request's properties[].propertyType to the matching family to keep them consistent.
export const COLLATERAL_TYPES = [
  { code: "01", family: "L" },
  { code: "02", family: "LB" },
  { code: "08", family: "U" },
];

export const FIRST_NAMES = [
  "Somchai", "Suda", "Anan", "Nattha", "Prasert", "Kanya", "Wichai", "Malee",
  "Chai", "Ploy", "Nirun", "Siri", "Arthit", "Wanida", "Decha", "Pim",
  "Krit", "Orn", "Tanat", "Bua", "Somsak", "Sunisa", "Thanawat", "Kanokwan",
  "Phakphum", "Suchada", "Anucha", "Nattaya", "Prawit", "Kamol", "Wirat", "Manee",
  "Chaiwat", "Ploypailin", "Niran", "Sirintra", "Arthorn", "Wandee", "Decho", "Pimchanok",
  "Kittipong", "Ornanong", "Tanapon", "Buppha", "Surasak", "Sunee", "Thaksin", "Kanda",
  "Phongsak", "Suthida", "Anon", "Natnicha", "Prasit", "Kamonchanok", "Wichian", "Maliwan",
  "Chaowalit", "Ploychompoo", "Nopparat", "Sirikanya", "Atchara", "Wannisa", "Detchai", "Pimrada",
  "Kittisak", "Orawan", "Thanakorn", "Busaba", "Suriya", "Supaporn", "Thawatchai", "Kessara",
  "Phisit", "Sukanya", "Apichat", "Naruemon", "Pramote", "Kanlaya", "Witthaya", "Mayuree",
  "Chalermchai", "Pornthip", "Narongchai", "Siriporn", "Adisorn", "Waraporn", "Direk", "Pichaya",
  "Jirayu", "Onuma", "Theerapong", "Benjawan", "Sittichai", "Saowalak", "Ekkachai", "Kullanit",
  "Phuwadon", "Sasithorn", "Anuwat", "Nittaya", "Preecha", "Kanyarat", "Worawut", "Montha",
  "Chakrit", "Patcharee", "Nat", "Suphalak", "Apinya", "Wassana", "Danai", "Phailin",
];
// Thai surnames are compound and highly diverse, so we COMPOSE them from a prefix
// syllable + a suffix syllable. With ~60 × ~60 that is ~3,600 distinct surnames, and
// FIRST_NAMES (~112) × surnames ≈ 400k+ distinct full names — comfortably enough to
// fill 100k records with little repetition while staying realistic-looking.
export const SURNAME_PREFIXES = [
  "Sri", "Boon", "Charoen", "Thong", "Suk", "Wong", "Rat", "Phong", "Chai", "Kit",
  "Mongkol", "Saeng", "Inthra", "Mani", "Rung", "Tham", "Wira", "Decha", "Naree", "Phan",
  "Pim", "Som", "Thawee", "Udom", "Wattana", "Anan", "Bunma", "Chan", "Kaew", "Lert",
  "Prom", "Ratcha", "Sin", "Tana", "Wich", "Sukh", "Nopp", "Phra", "Sa-ard", "Yotha",
  "Chaiya", "Kanok", "Maneer", "Phaibun", "Rojana", "Suwan", "Thira", "Wisut", "Amporn", "Buri",
  "Chinna", "Kamol", "Naruem", "Pichai", "Sangwan", "Thanya", "Wong-", "Aroon", "Boribun", "Detch",
];
export const SURNAME_SUFFIXES = [
  "wong", "sak", "chai", "phan", "suk", "dee", "rat", "korn", "sri", "thong",
  "charoen", "phong", "kul", "sombat", "rak", "song", "phon", "wat", "chana", "mongkol",
  "kit", "phisut", "anan", "sawat", "yan", "krit", "rung", "sin", "thai", "nan",
  "phirom", "suwan", "chu", "kham", "lert", "prasit", "ruang", "yothin", "chot", "bun",
  "mani", "phak", "rote", "sap", "thep", "wan", "yong", "achai", "intra", "phol",
  "klang", "muang", "ngam", "porn", "rit", "suwat", "wongse", "akorn", "amnuay", "boonma",
];

// --- helpers (k6/JS, no Date.now/Math.random restrictions here — this is plain k6 runtime) ---

export function pick(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

export function randDigits(n) {
  let s = "";
  for (let i = 0; i < n; i++) s += Math.floor(Math.random() * 10);
  return s;
}

// Returns a synchronized { subDistrict, district, province, postcode } from the pool.
export function randomGeo() {
  const [code, postcode] = pick(GEO);
  return {
    subDistrict: code,
    district: code.slice(0, 4),
    province: code.slice(0, 2),
    postcode: postcode,
  };
}

// Compose a surname from a prefix + suffix syllable (e.g. "Sri"+"wong" => "Sriwong").
export function randomLastName() {
  return pick(SURNAME_PREFIXES) + pick(SURNAME_SUFFIXES);
}

export function randomName() {
  return `${pick(FIRST_NAMES)} ${randomLastName()}`;
}

export function randomPhone() {
  return "08" + randDigits(8);
}

// A selling price in a realistic range (0.5M – 30M, rounded to 10k).
export function randomPrice() {
  const v = 500000 + Math.floor(Math.random() * 2950) * 10000;
  return v;
}
