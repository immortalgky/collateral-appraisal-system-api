namespace Shared.Messaging.Values;

public enum TaskName
{
    RequestMaker,
    Admin,
    ExtAppraisalStaff,
    ExtAppraisalChecker,
    ExtAppraisalVerifier,
    IntAppraisalStaff,
    IntAppraisalChecker,
    IntAppraisalVerifier,
    PendingApproval
}

public static class TaskNameExtensions
{
    public static TaskName? FromActivityId(string activityId)
    {
        return activityId switch
        {
            "request-maker" => TaskName.RequestMaker,
            "admin" => TaskName.Admin,
            "ext-appraisal-staff" => TaskName.ExtAppraisalStaff,
            "ext-appraisal-checker" => TaskName.ExtAppraisalChecker,
            "ext-appraisal-verifier" => TaskName.ExtAppraisalVerifier,
            "int-appraisal-staff" => TaskName.IntAppraisalStaff,
            "int-appraisal-checker" => TaskName.IntAppraisalChecker,
            "int-appraisal-verifier" => TaskName.IntAppraisalVerifier,
            "pending-approval" => TaskName.PendingApproval,
            _ => null
        };
    }
}