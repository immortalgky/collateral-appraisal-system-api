using Appraisal.Domain.Evaluations;
using Microsoft.EntityFrameworkCore;
using Shared.Data.Seed;

namespace Appraisal.Infrastructure.Seed;

/// <summary>
/// Seeds 10 EvaluationCriteriaConfig rows: 5 criteria × 2 segments (Retail, IBG).
/// Guarded — skips entirely if any row exists.
/// Guidance texts differ by segment for slots 1 and 2; slots 3–5 are identical for both.
/// ThresholdsJson is non-null only for slot 2 (Delivery).
/// </summary>
public class EvaluationCriteriaConfigDataSeed(
    AppraisalDbContext ctx,
    ILogger<EvaluationCriteriaConfigDataSeed> logger)
    : IDataSeeder<AppraisalDbContext>
{
    public async Task SeedAllAsync()
    {
        if (await ctx.EvaluationCriteriaConfigs.AnyAsync())
        {
            logger.LogInformation("EvaluationCriteriaConfigs already seeded, skipping.");
            return;
        }

        logger.LogInformation("Seeding EvaluationCriteriaConfigs...");

        // ── Shared guidance for slots 3, 4, 5 ────────────────────────────────

        const string slot3Guidance = """
            {
              "1": { "en": "Should be improved", "th": "ควรปรับปรุง" },
              "2": { "en": "Fairly prepared", "th": "จัดเตรียมได้พอใช้" },
              "3": { "en": "Moderately prepared", "th": "จัดเตรียมได้ปานกลาง" },
              "4": { "en": "Well prepared", "th": "จัดเตรียมได้ดี" },
              "5": { "en": "Very well prepared.", "th": "จัดเตรียมได้ดีมาก" }
            }
            """;

        const string slot4Guidance = """
            {
              "1": { "en": "Fix issues in reports in more than 60 minutes", "th": "แก้ไขปัญหาในรายงาน มากกว่า 60 นาที" },
              "2": { "en": "Fix the problem in the report within 60 minutes.", "th": "แก้ไขปัญหาในรายงาน ไม่เกิน 60 นาที" },
              "3": { "en": "Fix the problem in the report within 50 minutes.", "th": "แก้ไขปัญหาในรายงาน ไม่เกิน 50 นาที" },
              "4": { "en": "Fix the problem in the report within 40 minutes.", "th": "แก้ไขปัญหาในรายงาน ไม่เกิน 40 นาที" },
              "5": { "en": "Fix the problem in the report within 30 minutes.", "th": "แก้ไขปัญหาในรายงาน ไม่เกิน 30 นาที" }
            }
            """;

        const string slot5Guidance = """
            {
              "1": { "en": "It is a level that should be improved.", "th": "อยู่ในระดับที่ควรปรับปรุง" },
              "2": { "en": "It is at a fairly good level.", "th": "อยู่ในระดับที่พอใช้" },
              "3": { "en": "It is at a moderate level.", "th": "อยู่ในระดับที่ปานกลาง" },
              "4": { "en": "At a good level", "th": "อยู่ในระดับที่ดี" },
              "5": { "en": "It is at a very good level.", "th": "อยู่ในระดับที่ดีมาก" }
            }
            """;

        // ── Retail-specific guidance ──────────────────────────────────────────

        const string retailSlot1Guidance = """
            {
              "1": { "en": "Create a new evaluation book", "th": "จัดทำเล่มประเมินเป็นใหม่" },
              "2": { "en": "There are more than 5 points of correction in the report.", "th": "มีการแก้ไขเล่มรายงาน มากกว่า 5 จุด" },
              "3": { "en": "There were 3-4 corrections to the report.", "th": "มีการแก้ไขเล่มรายงาน 3-4 จุด" },
              "4": { "en": "There are 1-2 corrections to the report.", "th": "มีการแก้ไขเล่มรายงาน 1-2 จุด" },
              "5": { "en": "There has been no revision of the report.", "th": "ไม่มีการแก้ไขเล่มรายงานเลย" }
            }
            """;

        const string retailSlot2Guidance = """
            {
              "1": { "en": "Can deliver the report book > 3.5 days", "th": "สามารถส่งเล่มรายงาน > 3.5 วัน" },
              "2": { "en": "Report book can be delivered within ≤ 3-3.5 days.", "th": "สามารถส่งเล่มรายงาน ≤ 3-3.5 วัน" },
              "3": { "en": "Report book can be delivered within ≤ 2.5-3 days.", "th": "สามารถส่งเล่มรายงาน ≤ 2.5-3 วัน" },
              "4": { "en": "Report book can be delivered within ≤ 2-2.5 days.", "th": "สามารถส่งเล่มรายงาน ≤ 2-2.5 วัน" },
              "5": { "en": "The report book can be delivered within 2 days.", "th": "สามารถส่งเล่มรายงาน ภายใน 2 วัน" }
            }
            """;

        // ── IBG-specific guidance ─────────────────────────────────────────────

        const string ibgSlot1Guidance = """
            {
              "1": { "en": "More than 15 points", "th": "มากกว่า 15 จุดขึ้นไป" },
              "2": { "en": "There are more than 10-15 points of correction in the report.", "th": "มีการแก้ไขเล่มรายงาน มากกว่า 10-15 จุด" },
              "3": { "en": "There are 5-10 points of correction in the report.", "th": "มีการแก้ไขเล่มรายงาน 5-10 จุด" },
              "4": { "en": "There are 1-5 points of correction in the report.", "th": "มีการแก้ไขเล่มรายงาน 1-5 จุด" },
              "5": { "en": "There has been no revision of the report.", "th": "ไม่มีการแก้ไขเล่มรายงานเลย" }
            }
            """;

        const string ibgSlot2Guidance = """
            {
              "1": { "en": "Can deliver the report book > 12 days", "th": "สามารถส่งเล่มรายงาน > 12 วัน" },
              "2": { "en": "Report book can be delivered within ≤ 10-12 days.", "th": "สามารถส่งเล่มรายงาน ≤ 10-12 วัน" },
              "3": { "en": "Report book can be delivered within ≤ 7-10 days.", "th": "สามารถส่งเล่มรายงาน ≤ 7-10 วัน" },
              "4": { "en": "Report book can be delivered within ≤ 5-7 days.", "th": "สามารถส่งเล่มรายงาน ≤ 5-7 วัน" },
              "5": { "en": "The report book can be delivered within 5 days.", "th": "สามารถส่งเล่มรายงาน ภายใน 5 วัน" }
            }
            """;

        // ── Delivery thresholds ───────────────────────────────────────────────
        // rating -> upper-bound business days; rating 1 has no upper bound (handled as fallback)
        const string retailDeliveryThresholds = """{"5":2,"4":2.5,"3":3,"2":3.5}""";
        const string ibgDeliveryThresholds    = """{"5":5,"4":7,"3":10,"2":12}""";

        // ── Build rows ────────────────────────────────────────────────────────

        var configs = new List<EvaluationCriteriaConfig>
        {
            // ── Retail ────────────────────────────────────────────────────────

            EvaluationCriteriaConfig.Create(
                bankingSegment: "Retail",
                criteriaSlot: 1,
                criteriaKey: "ReportBookQuality",
                labelEn: "Report book quality",
                labelTh: "คุณภาพรูปเล่มรายงาน",
                weight: 0.20m,
                maxScore: 5,
                guidanceJson: retailSlot1Guidance,
                thresholdsJson: null,
                displayOrder: 1),

            EvaluationCriteriaConfig.Create(
                bankingSegment: "Retail",
                criteriaSlot: 2,
                criteriaKey: "Delivery",
                labelEn: "Delivery time",
                labelTh: "ระยะเวลาในการส่งมอบ",
                weight: 0.50m,
                maxScore: 5,
                guidanceJson: retailSlot2Guidance,
                thresholdsJson: retailDeliveryThresholds,
                displayOrder: 2),

            EvaluationCriteriaConfig.Create(
                bankingSegment: "Retail",
                criteriaSlot: 3,
                criteriaKey: "Personnel",
                labelEn: "Preparing the company's personnel for accepting bank assessment work",
                labelTh: "การจัดเตรียมบุคลากรของบริษัทต่อการรับงานประเมินธนาคาร",
                weight: 0.10m,
                maxScore: 5,
                guidanceJson: slot3Guidance,
                thresholdsJson: null,
                displayOrder: 3),

            EvaluationCriteriaConfig.Create(
                bankingSegment: "Retail",
                criteriaSlot: 4,
                criteriaKey: "Response",
                labelEn: "Response time to problem resolution",
                labelTh: "ระยะเวลาการตอบสนองต่อการแก้ปัญหา",
                weight: 0.10m,
                maxScore: 5,
                guidanceJson: slot4Guidance,
                thresholdsJson: null,
                displayOrder: 4),

            EvaluationCriteriaConfig.Create(
                bankingSegment: "Retail",
                criteriaSlot: 5,
                criteriaKey: "Coordination",
                labelEn: "Coordination and responsibility in work",
                labelTh: "การประสานงานและความรับผิดชอบในงาน",
                weight: 0.10m,
                maxScore: 5,
                guidanceJson: slot5Guidance,
                thresholdsJson: null,
                displayOrder: 5),

            // ── IBG ───────────────────────────────────────────────────────────

            EvaluationCriteriaConfig.Create(
                bankingSegment: "IBG",
                criteriaSlot: 1,
                criteriaKey: "ReportBookQuality",
                labelEn: "Report book quality",
                labelTh: "คุณภาพรูปเล่มรายงาน",
                weight: 0.40m,
                maxScore: 5,
                guidanceJson: ibgSlot1Guidance,
                thresholdsJson: null,
                displayOrder: 1),

            EvaluationCriteriaConfig.Create(
                bankingSegment: "IBG",
                criteriaSlot: 2,
                criteriaKey: "Delivery",
                labelEn: "Delivery time",
                labelTh: "ระยะเวลาในการส่งมอบ",
                weight: 0.30m,
                maxScore: 5,
                guidanceJson: ibgSlot2Guidance,
                thresholdsJson: ibgDeliveryThresholds,
                displayOrder: 2),

            EvaluationCriteriaConfig.Create(
                bankingSegment: "IBG",
                criteriaSlot: 3,
                criteriaKey: "Personnel",
                labelEn: "Preparing the company's personnel for accepting bank assessment work",
                labelTh: "การจัดเตรียมบุคลากรของบริษัทต่อการรับงานประเมินธนาคาร",
                weight: 0.10m,
                maxScore: 5,
                guidanceJson: slot3Guidance,
                thresholdsJson: null,
                displayOrder: 3),

            EvaluationCriteriaConfig.Create(
                bankingSegment: "IBG",
                criteriaSlot: 4,
                criteriaKey: "Response",
                labelEn: "Response time to problem resolution",
                labelTh: "ระยะเวลาการตอบสนองต่อการแก้ปัญหา",
                weight: 0.10m,
                maxScore: 5,
                guidanceJson: slot4Guidance,
                thresholdsJson: null,
                displayOrder: 4),

            EvaluationCriteriaConfig.Create(
                bankingSegment: "IBG",
                criteriaSlot: 5,
                criteriaKey: "Coordination",
                labelEn: "Coordination and responsibility in work",
                labelTh: "การประสานงานและความรับผิดชอบในงาน",
                weight: 0.10m,
                maxScore: 5,
                guidanceJson: slot5Guidance,
                thresholdsJson: null,
                displayOrder: 5),
        };

        ctx.EvaluationCriteriaConfigs.AddRange(configs);
        await ctx.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} EvaluationCriteriaConfig rows", configs.Count);
    }
}
