#!/usr/bin/env python3
"""
Generate a sample AS400 COLLATREV file for testing the Periodical Reappraisal (AS400) ingestion job.

SUPERSEDED (dev): prefer the in-app generator that builds a file from REAL completed appraisals so the
ingestion job's SurveyNumber -> appraisal.Appraisals.AppraisalNumber join (lat/lon enrichment + prior-
appraisal linkage) actually fires:

    POST https://localhost:7111/reappraisal/generate-test-file?count=20[&date=YYYYMMDD]
    (requires the REAPPRAISAL_GENERATE_TEST_FILE permission; also available as a menu item for QA)

Keep this script only for offline / no-database scenarios where synthetic rows are good enough.

The file is the external AS400 → CAS interface ("Collateral Review Interface"): a **fixed-width**
UTF-8 text file with Header (H) / Detail (D) / Trailer (T) records. Each Detail record is
exactly **640 characters** (Unicode code-points, not bytes — matches CollatrevFileParser).

    H + EffectiveDate(DDMMYYYY, 8) + filler                    → 640 chars
    D + 34 fields padded to their widths (alpha left, numeric right) → 640 chars
    T + DetailCount(9, right-aligned) + filler                 → 640 chars

Usage:
    python3 generate_collatrev.py [YYYYMMDD] [--out DIR] [--survey1 APPRAISAL_NUMBER]

    YYYYMMDD       file date used in the filename (default: 20260501)
    --out DIR      output directory (default: current dir). Created if missing.
    --survey1 V    Survey No for row 1 (default "68A000001"). Set this to a REAL
                   appraisal.Appraisals.AppraisalNumber so lat/lon enrichment populates.
"""
import argparse
import os
import sys

RECORD_LEN = 660

# (name, width, align)  align: 'L' = left (alpha), 'R' = right (numeric)
# RecordType is always 'D' for detail rows; we keep it as the first field for clarity.
DETAIL_FIELDS = [
    ("RecordType",            1,   "L"),
    ("ReviewType",            1,   "L"),
    ("ReviewDate",            8,   "L"),   # DDMMYYYY
    ("CollateralId",          19,  "R"),
    ("SurveyNo",              10,  "L"),
    ("CollateralCode",        3,   "L"),
    ("CollateralCategory",    3,   "L"),
    ("CollateralName",        30,  "L"),
    ("CollateralAddress",     100, "L"),
    ("CifNo",                 19,  "R"),
    ("CifName",               20,  "L"),
    ("AoCode",                10,  "L"),
    ("AoName",                40,  "L"),
    ("TitleNo",               20,  "L"),
    ("CurrentValue",          16,  "R"),   # dec15,2 — right-aligned
    ("ValuationDate",         8,   "L"),   # DDMMYYYY
    ("InternalExternal",      1,   "L"),
    ("BusinessSize",          1,   "L"),
    ("BusinessSizeDesc",      40,  "L"),
    ("MortgageAmount",        16,  "R"),
    ("PastDueDay",            5,   "R"),
    ("ApplicationNo",         19,  "R"),
    ("FacilityCode",          3,   "L"),
    ("FacilitySequence",      19,  "R"),
    ("CpNumber",              16,  "L"),
    ("CarCode",               3,   "L"),
    ("FacilityLimit",         16,  "R"),
    ("FlagLessAge4Y",         1,   "L"),
    ("FlagGreaterAge4Y",      1,   "L"),
    ("CountAgeingDate",       10,  "L"),
    ("CollateralDescription", 50,  "L"),
    ("ExternalValuerName",    40,  "L"),
    ("InternalValuerName",    40,  "L"),
    ("SllOver100M",           1,   "L"),
    ("SllDescription",        50,  "L"),
    # Trailing extension fields (pos 641–660; record extends 640 → 660).
    ("Stage",                 1,   "L"),
    ("IBGRetail",             10,  "L"),
    ("Group",                 1,   "L"),
    ("EffectiveDateAppraisal", 8,  "L"),  # DDMMYYYY
]

# Sanity: detail widths must sum to 640.
assert sum(w for _, w, _ in DETAIL_FIELDS) == RECORD_LEN, \
    f"DETAIL_FIELDS widths sum to {sum(w for _, w, _ in DETAIL_FIELDS)}, expected {RECORD_LEN}"


def pad(value, width, align):
    s = "" if value is None else str(value)
    if len(s) > width:
        s = s[:width]                       # truncate over-long values to keep alignment
    return s.rjust(width) if align == "R" else s.ljust(width)


def build_detail(row):
    out = "".join(pad(row.get(name, ""), width, align) for name, width, align in DETAIL_FIELDS)
    assert len(out) == RECORD_LEN, f"Detail length {len(out)} != {RECORD_LEN}"
    return out


def build_header(effective_ddmmyyyy):
    return ("H" + pad(effective_ddmmyyyy, 8, "L")).ljust(RECORD_LEN)


def build_trailer(count):
    # pos 1 = 'T', pos 2–10 = 9-char detail count (right-aligned), rest filler.
    return ("T" + pad(count, 9, "R")).ljust(RECORD_LEN)


def sample_rows(survey1):
    return [
        # Row 1 — Normal Review, ASCII, FUTURE ReviewDate (positive Remaining Days).
        # SurveyNo should match a real appraisal.Appraisals.AppraisalNumber for geo enrichment.
        {
            "RecordType": "D", "ReviewType": "1", "ReviewDate": "01122026",
            "CollateralId": "55076", "SurveyNo": survey1,
            "CollateralCode": "11A", "CollateralCategory": "RE",
            "CollateralName": "Sample Land Plot A",
            "CollateralAddress": "123 Sukhumvit Rd, Khlong Toei, Bangkok 10110",
            "CifNo": "68057984", "CifName": "Somchai Jaidee",
            "AoCode": "F85", "AoName": "Komsan Munmin",
            "TitleNo": "NS3K-1827", "CurrentValue": "4500000.00",
            "ValuationDate": "28112019", "InternalExternal": "I",
            "BusinessSize": "C", "BusinessSizeDesc": "SME Standard",
            "MortgageAmount": "4000000.00", "PastDueDay": "0",
            "ApplicationNo": "68057984", "FacilityCode": "OD",
            "FacilitySequence": "1", "CpNumber": "68057984OD", "CarCode": "A",
            "FacilityLimit": "4000000.00", "FlagLessAge4Y": "", "FlagGreaterAge4Y": "Y",
            "CountAgeingDate": "4/9", "CollateralDescription": "Overdraft facility",
            "ExternalValuerName": "", "InternalValuerName": "Bank Appraiser 1",
            "SllOver100M": "N", "SllDescription": "Internal appraisal <=100M",
            "Stage": "1", "IBGRetail": "Retail", "Group": "1", "EffectiveDateAppraisal": "28112019",
        },
        # Row 2 — Before Stage 3, THAI text, PAST ReviewDate (overdue), SurveyNo with no in-system match.
        {
            "RecordType": "D", "ReviewType": "2", "ReviewDate": "15012025",
            "CollateralId": "55077", "SurveyNo": "6800002",
            "CollateralCode": "12B", "CollateralCategory": "RE",
            "CollateralName": "คอนโดสุขุมวิท ชั้น 8",
            "CollateralAddress": "ติดถนนสายกาฬสินธุ์-สมเด็จ ต.เหนือ อ.เมือง จ.กาฬสินธุ์",
            "CifNo": "68057985", "CifName": "วิภา ใจดี",
            "AoCode": "F86", "AoName": "สมหญิง รักงาน",
            "TitleNo": "NS3K-1828", "CurrentValue": "2750000.00",
            "ValuationDate": "10012020", "InternalExternal": "E",
            "BusinessSize": "C", "BusinessSizeDesc": "SME มาตรฐาน",
            "MortgageAmount": "2500000.00", "PastDueDay": "60",
            "ApplicationNo": "68057985", "FacilityCode": "TL",
            "FacilitySequence": "2", "CpNumber": "68057985TL", "CarCode": "B",
            "FacilityLimit": "2500000.00", "FlagLessAge4Y": "Y", "FlagGreaterAge4Y": "",
            "CountAgeingDate": "5/2", "CollateralDescription": "คอนโดมิเนียม",
            "ExternalValuerName": "ABC Valuer Co Ltd", "InternalValuerName": "",
            "SllOver100M": "N", "SllDescription": "ประเมินภายในสำหรับที่ไม่เกิน 100 ล้าน",
            "Stage": "2", "IBGRetail": "IBG", "Group": "2", "EffectiveDateAppraisal": "10012020",
        },
        # Row 3 — Stage 3, minimal/blank optional fields (null handling), SllOver100M = Y.
        {
            "RecordType": "D", "ReviewType": "3", "ReviewDate": "01062026",
            "CollateralId": "55078", "SurveyNo": "6800003",
            "CollateralCode": "11A", "CollateralCategory": "RE",
            "CollateralName": "Sample Land Plot C", "CollateralAddress": "",
            "CifNo": "68057986", "CifName": "Anan Poommin",
            "AoCode": "", "AoName": "", "TitleNo": "", "CurrentValue": "",
            "ValuationDate": "", "InternalExternal": "I", "BusinessSize": "",
            "BusinessSizeDesc": "", "MortgageAmount": "", "PastDueDay": "",
            "ApplicationNo": "", "FacilityCode": "", "FacilitySequence": "",
            "CpNumber": "", "CarCode": "", "FacilityLimit": "", "FlagLessAge4Y": "",
            "FlagGreaterAge4Y": "Y", "CountAgeingDate": "", "CollateralDescription": "",
            "ExternalValuerName": "", "InternalValuerName": "",
            "SllOver100M": "Y", "SllDescription": "External appraisal >100M",
            "Stage": "3", "IBGRetail": "Retail", "Group": "3", "EffectiveDateAppraisal": "",
        },
    ]


def main():
    ap = argparse.ArgumentParser(description="Generate a sample AS400 COLLATREV fixed-width file.")
    ap.add_argument("date", nargs="?", default="20260501", help="file date YYYYMMDD")
    ap.add_argument("--out", default=".", help="output directory")
    ap.add_argument("--survey1", default="68A000001",
                    help="Survey No for row 1 (use a real AppraisalNumber for geo enrichment)")
    args = ap.parse_args()

    if len(args.date) != 8 or not args.date.isdigit():
        sys.exit("date must be YYYYMMDD (8 digits)")

    # Header EffectiveDate is DDMMYYYY; filename date is YYYYMMDD — convert.
    yyyy, mm, dd = args.date[:4], args.date[4:6], args.date[6:8]
    effective_ddmmyyyy = f"{dd}{mm}{yyyy}"

    rows = sample_rows(args.survey1)
    lines = [build_header(effective_ddmmyyyy)]
    lines += [build_detail(r) for r in rows]
    lines.append(build_trailer(len(rows)))

    os.makedirs(args.out, exist_ok=True)
    path = os.path.join(args.out, f"AS400_COLLATREV_{args.date}.txt")
    # UTF-8 without BOM; CRLF line endings (host files are typically CRLF, parser handles both).
    with open(path, "w", encoding="utf-8", newline="\r\n") as f:
        f.write("\n".join(lines) + "\n")

    print(f"Wrote {path} ({len(rows)} detail rows, fixed-width {RECORD_LEN} chars)")


if __name__ == "__main__":
    main()
