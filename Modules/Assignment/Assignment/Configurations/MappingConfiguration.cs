using Assignment.Assignments.Features.CreateAssignment;

namespace Assignment.Configurations;

public static class MappingConfiguration
{
    public static void ConfigureMappings()
    {
        TypeAdapterConfig<AssignmentDetailDto, AssignmentDetail>
            .NewConfig()
            .ConstructUsing(src => AssignmentDetail.Create(
                src.ReqID,
                src.AssignmentMethod,
                src.ExternalCompanyID,
                src.ExternalCompanyAssignType,
                src.ExtApprStaff,
                src.ExtApprStaffAssignmentType,
                src.IntApprStaff,
                src.IntApprStaffAssignmentType,
                src.Remark
            ));

    }
}