namespace Backend.Services.Caching;

public interface IFplCacheCoordinator
{
    Task<T> GetOrCreateAsync<T>(
        FplCachePolicy policy,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken);
}