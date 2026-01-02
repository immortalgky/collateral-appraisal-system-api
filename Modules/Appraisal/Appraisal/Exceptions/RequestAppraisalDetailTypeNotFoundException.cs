namespace Appraisal.Exceptions;

public class RequestAppraisalDetailTypeNotFoundException(string type) : NotFoundException("RequestAppraisalDetailType", type)
{
}