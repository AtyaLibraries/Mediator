namespace Atya.Application.Mediator;

/// <summary>
/// Specifies the dependency injection lifetime of a mediator request handler.
/// </summary>
public enum MediatorHandlerLifetime
{
    /// <summary>A new handler instance is created for every resolution. This is the default.</summary>
    Transient = 0,

    /// <summary>One handler instance is shared within a dependency injection scope.</summary>
    Scoped = 1,

    /// <summary>One handler instance is shared for the lifetime of the container.</summary>
    Singleton = 2,
}

/// <summary>
/// Declares the dependency injection lifetime the source generator uses when it registers the annotated handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class MediatorHandlerLifetimeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MediatorHandlerLifetimeAttribute"/> class.
    /// </summary>
    /// <param name="lifetime">The handler lifetime.</param>
    public MediatorHandlerLifetimeAttribute(MediatorHandlerLifetime lifetime)
    {
        Lifetime = lifetime;
    }

    /// <summary>
    /// Gets the handler lifetime.
    /// </summary>
    public MediatorHandlerLifetime Lifetime { get; }
}
