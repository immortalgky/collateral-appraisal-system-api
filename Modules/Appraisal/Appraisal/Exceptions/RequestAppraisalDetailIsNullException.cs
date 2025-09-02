namespace Appraisal.Exceptions;

public class RequestAppraisalDetailIsNullException(string type) : NotFoundException("RequestAppraisalDetailIsNull", type)
{
}