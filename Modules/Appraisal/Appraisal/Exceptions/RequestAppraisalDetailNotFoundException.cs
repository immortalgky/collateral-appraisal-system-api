namespace Appraisal.Exceptions;

public class RequestAppraisalDetailNotFoundException(long id) : NotFoundException("RequestAppraisalDetail", id)
{
}