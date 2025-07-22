namespace Parameter.Parameters.Exceptions;

public class ParameterNotFoundException(ParameterDto parameter) : NotFoundException("Parameter", parameter)
{
}