namespace Appraisal.Exceptions;

using Shared.Exceptions;

public class RequestAppraisalDetailTypeNotFoundException(string type) : NotFoundException("RequestAppraisalDetailType", type)
{
}