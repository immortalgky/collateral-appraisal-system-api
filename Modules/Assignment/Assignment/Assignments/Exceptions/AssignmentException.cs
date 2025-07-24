using Shared.Exceptions;

namespace Assignment.Assignments.Exceptions;

public class AssignmentNotFoundException(long id) : NotFoundException("Assignment", id)
{
}