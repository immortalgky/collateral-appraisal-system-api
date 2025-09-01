namespace Appraisal.Exceptions;

using Shared.Exceptions;

public class RequestAppraisalDetailIsNullException(string type) : NotFoundException("RequestAppraisalDetailIsNull", type)
{
}