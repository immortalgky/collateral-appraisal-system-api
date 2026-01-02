namespace Request.Contracts.Requests.Dtos;

public record RequestorDto(
    string RequestorEmpId,
    string RequestorName,
    string RequestorEmail,
    string RequestorContactNo,
    string RequestorAo,
    string RequestorBranch,
    string RequestorBusinessUnit,
    string RequestorDepartment,
    string RequestorSection,
    string RequestorCostCenter
);