namespace Shared.DDD;

public interface IExternalized<out TIntegrationEvent>
{
    TIntegrationEvent ToIntegrationEvent();
}