using System.Reflection;

namespace O9d.AspNet.FluentValidation;

/// <summary>
/// Represents a strategy used to determine whether the provided endpoint details are validateable.
/// </summary>
/// <param name="parameterInfo">The endpoint parameter</param>
/// <param name="endpointMetadata">The endpoint metadata</param>
/// <returns>True if the parameter is validateable, otherwise False</returns>
public delegate bool ValidationStrategy(ParameterInfo parameterInfo, IList<object> endpointMetadata);

/// <summary>
/// Built-in strategies for determining if an endpoint parameter is validateable
/// </summary>
public static class ValidationStrategies
{
    /// <summary>
    /// Validation strategy that checks for a presence of the <see cref="ValidateAttribute"/>
    /// </summary>
    public static readonly ValidationStrategy HasValidateAttribute = (pi, _)
        => pi.GetCustomAttribute<ValidateAttribute>(true) is not null;

    /// <summary>
    /// Validation strategy that checks for the presence of <see cref="EndpointValidationMetadata"/> metadata on the endpoint.
    /// </summary>
    public static readonly ValidationStrategy HasValidationMetadata = (pi, endpointMetadata) =>
    {
        foreach (var metadata in endpointMetadata)
        {
            if (metadata is EndpointValidationMetadata endpointValidationMetadata)
            {
                return endpointValidationMetadata.TypesToValidate.Contains(pi.ParameterType);
            }
        }

        return false;
    };

    /// <summary>
    /// Creates a validation strategy that checks if the parameter type derives from the specified type
    /// </summary>
    /// <typeparam name="T">The base type to check</typeparam>
    public static ValidationStrategy DerivesFrom<T>()
        => (pi, _) => pi.ParameterType.IsAssignableTo(typeof(T));
}