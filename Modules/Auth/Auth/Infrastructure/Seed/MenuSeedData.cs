using Auth.Domain.Menu;

namespace Auth.Infrastructure.Seed;

/// <summary>
/// Seed blueprint for the DB-driven navigation menu. Mirrors the legacy
/// hardcoded frontend arrays in
/// src/shared/config/navigation.ts and src/shared/config/appraisalNavigation.ts.
/// Every entry here must be marked IsSystem=true; the application layer uses
/// the seeded ItemKeys to key into code-side showWhen predicates.
/// </summary>
public static class MenuSeedData
{
    public record MenuSeedNode(
        string ItemKey,
        string LabelEn,
        string IconName,
        IconStyle IconStyle,
        string? IconColor,
        string? Path,
        string ViewPermissionCode,
        string? EditPermissionCode,
        List<MenuSeedNode>? Children = null);

    public static List<MenuSeedNode> GetMainMenuSeed() => new()
    {
        new("main.dashboard", "Dashboard", "gauge", IconStyle.Solid, "text-blue-500", "/", "DASHBOARD_VIEW", null),
        new("main.request", "Request", "folder-open", IconStyle.Solid, "text-emerald-500", "/requests", "REQUEST_VIEW", null,
            new List<MenuSeedNode>
            {
                new("main.request.list", "Request Listing", "list", IconStyle.Solid, "text-emerald-500", "/requests", "REQUEST_VIEW", null),
                new("main.request.create", "Create Request", "file-circle-plus", IconStyle.Solid, "text-emerald-500", "/requests/new", "REQUEST_CREATE", "REQUEST_CREATE"),
            }),
        new("main.task", "Task", "list-check", IconStyle.Solid, "text-purple-500", "/tasks", "TASK_LIST_VIEW", null,
            new List<MenuSeedNode>
            {
                new("main.task.all", "All Tasks", "list", IconStyle.Solid, "text-purple-500", "/tasks", "TASK_LIST_VIEW", null),
                new("main.task.appraisal-initiation-check", "Appraisal Initiation Check", "clipboard-check", IconStyle.Solid, "text-purple-500", "/tasks?activityId=appraisal-initiation-check", "TASK_APPR_INITIATION_CHECK", null),
                new("main.task.appraisal-initiation", "Appraisal Initiation", "file-pen", IconStyle.Solid, "text-purple-500", "/tasks?activityId=appraisal-initiation", "TASK_APPR_INITIATION", null),
                new("main.task.appraisal-assignment", "Appraisal Assignment", "building", IconStyle.Solid, "text-purple-500", "/tasks?activityId=appraisal-assignment", "TASK_APPR_ASSIGNMENT", null),
                new("main.task.ext-appraisal-assignment", "External Appraisal Assignment", "building-columns", IconStyle.Solid, "text-purple-500", "/tasks?activityId=ext-appraisal-assignment", "TASK_EXT_APPR_ASSIGNMENT", null),
                new("main.task.ext-appraisal-execution", "External Appraisal Execution", "user-tie", IconStyle.Solid, "text-purple-500", "/tasks?activityId=ext-appraisal-execution", "TASK_EXT_APPR_EXECUTION", null),
                new("main.task.ext-appraisal-check", "External Appraisal Check", "clipboard-check", IconStyle.Solid, "text-purple-500", "/tasks?activityId=ext-appraisal-check", "TASK_EXT_APPR_CHECK", null),
                new("main.task.ext-appraisal-verification", "External Appraisal Verification", "shield-check", IconStyle.Solid, "text-purple-500", "/tasks?activityId=ext-appraisal-verification", "TASK_EXT_APPR_VERIFICATION", null),
                new("main.task.appraisal-book-verification", "Appraisal Book Verification", "book-open", IconStyle.Solid, "text-purple-500", "/tasks?activityId=appraisal-book-verification", "TASK_APPR_BOOK_VERIFICATION", null),
                new("main.task.int-appraisal-execution", "Internal Appraisal Execution", "user", IconStyle.Solid, "text-purple-500", "/tasks?activityId=int-appraisal-execution", "TASK_INT_APPR_EXECUTION", null),
                new("main.task.int-appraisal-check", "Internal Appraisal Check", "magnifying-glass-check", IconStyle.Solid, "text-purple-500", "/tasks?activityId=int-appraisal-check", "TASK_INT_APPR_CHECK", null),
                new("main.task.int-appraisal-verification", "Internal Appraisal Verification", "badge-check", IconStyle.Solid, "text-purple-500", "/tasks?activityId=int-appraisal-verification", "TASK_INT_APPR_VERIFICATION", null),
                new("main.task.pending-approval", "Pending Approval", "hourglass-half", IconStyle.Solid, "text-purple-500", "/tasks?activityId=pending-approval", "TASK_PENDING_APPROVAL", null),
                new("main.task.provide-additional-documents", "Provide Additional Documents", "file-circle-plus", IconStyle.Solid, "text-purple-500", "/tasks?activityId=provide-additional-documents", "TASK_PROVIDE_ADDITIONAL_DOCS", null),
                new("main.task.ext-collect-submissions", "Submit Quotation", "paper-plane", IconStyle.Solid, "text-purple-500", "/tasks?activityId=ext-collect-submissions", "TASK_QUOTATION_SUBMIT", null),
                new("main.task.ext-respond-negotiation", "Respond to Negotiation", "comments-dollar", IconStyle.Solid, "text-purple-500", "/tasks?activityId=ext-respond-negotiation", "TASK_QUOTATION_NEGOTIATE", null),
                new("main.task.admin-review-submissions", "Review Quotation Bids", "magnifying-glass-chart", IconStyle.Solid, "text-purple-500", "/tasks?activityId=admin-review-submissions", "TASK_QUOTATION_REVIEW", null),
                new("main.task.rm-pick-winner", "Pick Quotation Winner", "medal", IconStyle.Solid, "text-purple-500", "/tasks?activityId=rm-pick-winner", "TASK_QUOTATION_PICK_WINNER", null),
                new("main.task.admin-finalize", "Finalize Quotation", "circle-check", IconStyle.Solid, "text-purple-500", "/tasks?activityId=admin-finalize", "TASK_QUOTATION_FINALIZE", null),
            }),
        new("main.appraisal", "Appraisal", "magnifying-glass-chart", IconStyle.Solid, "text-cyan-500", "/appraisals", "APPRAISAL_VIEW", null,
            new List<MenuSeedNode>
            {
                new("main.appraisal.search", "Search", "magnifying-glass", IconStyle.Solid, "text-cyan-500", "/appraisals/search", "APPRAISAL_VIEW", null),
                new("main.appraisal.my-appraisals", "My Appraisals", "folder-user", IconStyle.Solid, "text-cyan-500", "/appraisals/my-appraisals", "APPRAISAL_VIEW", null),
                new("main.appraisal.pending-review", "Pending Review", "clipboard-check", IconStyle.Solid, "text-amber-500", "/appraisals/pending-review", "APPRAISAL_REVIEW", null),
            }),
        new("main.quotation", "Quotation", "file-invoice-dollar", IconStyle.Solid, "text-pink-500", "/quotations", "QUOTATION_VIEW", null,
            new List<MenuSeedNode>
            {
                new("main.quotation.list", "All Quotations", "list", IconStyle.Solid, "text-pink-500", "/quotations", "QUOTATION_VIEW", null),
                new("main.quotation.drafts", "My Drafts", "file-pen", IconStyle.Solid, "text-pink-500", "/quotations/drafts", "QUOTATION_DRAFT_VIEW", "QUOTATION_DRAFT_EDIT"),
                new("main.quotation.external", "External Co. Portal", "building", IconStyle.Solid, "text-pink-500", "/ext/quotations", "QUOTATION_EXT_VIEW", null),
            }),
        new("main.reports", "Reports", "chart-line", IconStyle.Solid, "text-indigo-500", "/reports", "REPORT_VIEW", null,
            new List<MenuSeedNode>
            {
                new("main.reports.completed", "Completed Reports", "file-check", IconStyle.Solid, "text-indigo-500", "/reports/completed", "REPORT_VIEW", null),
                new("main.reports.statistics", "Statistics", "chart-pie", IconStyle.Solid, "text-indigo-500", "/reports/statistics", "REPORT_STATISTICS_VIEW", null),
            }),
        new("main.meetings", "Meetings", "people-arrows", IconStyle.Solid, "text-blue-500", "/meetings", "MEETING_MANAGE", null,
            new List<MenuSeedNode>
            {
                new("main.meetings.all", "All Meetings", "list", IconStyle.Solid, "text-blue-500", "/meetings", "MEETING_MANAGE", null),
                new("main.meetings.queue", "Awaiting Meeting Queue", "hourglass-half", IconStyle.Solid, "text-blue-500", "/meetings/queue", "MEETING_MANAGE", null),
            }),
        new("main.notification", "Notification", "bell", IconStyle.Solid, "text-amber-500", "/notifications", "DASHBOARD_VIEW", null),
        new("main.standalone", "Standalone", "puzzle-piece", IconStyle.Solid, "text-teal-500", "/standalone", "STANDALONE_USE", null),
        new("main.parameter", "Parameter", "sliders", IconStyle.Solid, "text-rose-500", "/parameter", "PARAMETER_MANAGE", null),
        new("main.user-management", "User Management", "users", IconStyle.Solid, "text-violet-500", "/users", "USER_MANAGE", null,
            new List<MenuSeedNode>
            {
                new("main.user-management.user-list", "User List", "list", IconStyle.Solid, "text-violet-500", "/users", "USER_MANAGE", null),
                new("main.user-management.role-assignment", "Role Assignment", "user-shield", IconStyle.Solid, "text-violet-500", "/users/roles", "USER_MANAGE", null),
                new("main.user-management.permissions", "Permissions", "shield-halved", IconStyle.Solid, "text-violet-500", "/admin/permissions", "PERMISSION_MANAGE", null),
                new("main.user-management.roles", "Roles", "user-shield", IconStyle.Solid, "text-violet-500", "/admin/roles", "ROLE_MANAGE", null),
                new("main.user-management.groups", "Groups", "users-rectangle", IconStyle.Solid, "text-violet-500", "/admin/groups", "GROUP_MANAGE", null),
                new("main.user-management.users", "Users", "circle-user", IconStyle.Solid, "text-violet-500", "/admin/users", "USER_MANAGE", null),
                new("main.user-management.menus", "Menus", "bars", IconStyle.Solid, "text-violet-500", "/admin/menus", "MENU_MANAGE", "MENU_MANAGE"),
            }),
        new("main.workflow-builder", "Workflow Builder", "diagram-project", IconStyle.Solid, "text-orange-500", "/workflow-builder", "WORKFLOW_MANAGE", "WORKFLOW_MANAGE",
            new List<MenuSeedNode>
            {
                new("main.workflow-builder.list", "Workflow Listing", "list", IconStyle.Solid, "text-orange-500", "/workflow-builder", "WORKFLOW_MANAGE", null),
                new("main.workflow-builder.create", "Create Workflow", "file-circle-plus", IconStyle.Solid, "text-orange-500", "/workflow-builder/new", "WORKFLOW_MANAGE", "WORKFLOW_MANAGE"),
            }),
        new("main.template-management", "Template Management", "layer-group", IconStyle.Solid, "text-teal-500", "/market-comparable-factors", "TEMPLATE_MANAGE", "TEMPLATE_MANAGE",
            new List<MenuSeedNode>
            {
                new("main.template-management.mc-factors", "MC Factors", "database", IconStyle.Solid, "text-teal-500", "/market-comparable-factors", "TEMPLATE_MANAGE", "TEMPLATE_MANAGE"),
                new("main.template-management.mc-templates", "MC Templates", "rectangle-list", IconStyle.Solid, "text-teal-500", "/market-comparable-templates", "TEMPLATE_MANAGE", "TEMPLATE_MANAGE"),
                new("main.template-management.comparative-templates", "Comparative Templates", "chart-mixed", IconStyle.Solid, "text-teal-500", "/comparative-templates", "TEMPLATE_MANAGE", "TEMPLATE_MANAGE"),
            }),
    };

    public static List<MenuSeedNode> GetAppraisalMenuSeed() => new()
    {
        // Mirror of applicationNavigation in appraisalNavigation.ts (1:1, same order)
        new("appraisal.360", "360 Summary", "compass", IconStyle.Solid, "text-teal-500",
            ":basePath/360", "APPRAISAL_360_VIEW", null),
        new("appraisal.request", "Request Information", "square-info", IconStyle.Solid, "text-emerald-500",
            ":basePath/request/:requestId", "APPRAISAL_REQUEST_VIEW", "APPRAISAL_REQUEST_EDIT"),
        new("appraisal.administration", "Administration", "user-tie", IconStyle.Solid, "text-indigo-500",
            ":basePath/administration", "APPRAISAL_ADMINISTRATION_VIEW", "APPRAISAL_ADMINISTRATION_EDIT"),
        new("appraisal.appointment", "Appointment & Fee", "calendar-check", IconStyle.Solid, "text-orange-500",
            ":basePath/appointment", "APPRAISAL_APPOINTMENT_VIEW", "APPRAISAL_APPOINTMENT_EDIT"),
        new("appraisal.quotation-submit", "Submit Quotation", "paper-plane", IconStyle.Solid, "text-pink-500",
            ":basePath/quotation/submit", "TASK_QUOTATION_SUBMIT", null),
        new("appraisal.quotation-respond-negotiation", "Respond to Negotiation", "comments-dollar", IconStyle.Solid, "text-pink-500",
            ":basePath/quotation/respond-negotiation", "TASK_QUOTATION_NEGOTIATE", null),
        new("appraisal.quotation-review", "Review Quotation Bids", "magnifying-glass-chart", IconStyle.Solid, "text-pink-500",
            ":basePath/quotation/review", "TASK_QUOTATION_REVIEW", null),
        new("appraisal.quotation-pick-winner", "Pick Quotation Winner", "medal", IconStyle.Solid, "text-pink-500",
            ":basePath/quotation/pick-winner", "TASK_QUOTATION_PICK_WINNER", null),
        new("appraisal.quotation-finalize", "Finalize Quotation", "circle-check", IconStyle.Solid, "text-pink-500",
            ":basePath/quotation/finalize", "TASK_QUOTATION_FINALIZE", null),
        new("appraisal.property", "Property Information", "buildings", IconStyle.Solid, "text-purple-500",
            ":basePath/property", "APPRAISAL_PROPERTY_VIEW", "APPRAISAL_PROPERTY_EDIT"),
        new("appraisal.block-condo", "Property Information (Condo)", "buildings", IconStyle.Solid, "text-purple-500",
            ":basePath/block-condo", "APPRAISAL_BLOCK_CONDO_VIEW", "APPRAISAL_BLOCK_CONDO_EDIT"),
        new("appraisal.block-village", "Property Information (Village)", "buildings", IconStyle.Solid, "text-purple-500",
            ":basePath/block-village", "APPRAISAL_BLOCK_VILLAGE_VIEW", "APPRAISAL_BLOCK_VILLAGE_EDIT"),
        new("appraisal.property-pma", "Property Information (PMA)", "buildings", IconStyle.Solid, "text-purple-500",
            ":basePath/property-pma", "APPRAISAL_PROPERTY_PMA_VIEW", "APPRAISAL_PROPERTY_PMA_EDIT"),
        new("appraisal.documents", "Document Checklist", "file-circle-check", IconStyle.Solid, "text-teal-500",
            ":basePath/documents", "APPRAISAL_DOCUMENTS_VIEW", "APPRAISAL_DOCUMENTS_EDIT"),
        new("appraisal.document-followup", "Provide Documents", "file-circle-plus", IconStyle.Solid, "text-amber-500",
            ":basePath/provide-documents", "TASK_PROVIDE_ADDITIONAL_DOCS", null),
        new("appraisal.summary", "Summary & Decision", "paper-plane", IconStyle.Solid, "text-sky-500",
            ":basePath/summary", "APPRAISAL_SUMMARY_VIEW", "APPRAISAL_SUMMARY_EDIT"),
    };
}
