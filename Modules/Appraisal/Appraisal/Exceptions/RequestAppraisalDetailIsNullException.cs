namespace Appraisal.Exceptions;

using Shared.Exceptions;

public class RequestAppraisalDetailIsNulLException(string type) : NotFoundException("RequestAppraisalDetailIsNull", type)
{
}