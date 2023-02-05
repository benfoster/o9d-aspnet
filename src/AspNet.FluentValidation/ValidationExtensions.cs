using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using O9d.AspNet.FluentValidation;

namespace Microsoft.AspNetCore.Builder;

public static class ValidationExtensions
{
    /// <summary>
    /// Indicates that endpoints created by the builder should be enlisted for validation.
    /// </summary>
    /// <param name="builder">The Microsoft.AspNetCore.Builder.IEndpointConventionBuilder.</param>
    /// <param name="typesToValidate">The parameter types to validate.</param>
    public static TBuilder RequireValidation<TBuilder>(this TBuilder builder, params Type[] typesToValidate) where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new EndpointValidationMetadata(typesToValidate));

    /// <summary>
    /// Adds a filter to validateable endpoints that uses Fluent Validation to validate input parameters.
    /// </summary>
    /// <param name="builder">The Microsoft.AspNetCore.Builder.IEndpointConventionBuilder.</param>
    /// <param name="configureOptions">A configuration expression to apply to the validation filter options.</param>
    public static TBuilder WithValidationFilter<TBuilder>(this TBuilder builder, Action<ValidationFilterOptions>? configureOptions = null) where TBuilder : IEndpointConventionBuilder
    {
        var options = new ValidationFilterOptions();
        configureOptions?.Invoke(options);

        // Use a convention so we can capture the endpoint's metadata and provide this to the validation strategy
        builder.Add(eb => eb.FilterFactories.Add(
            (ctx, next) => CreateValidationFilterFactory(options, eb.Metadata, ctx, next))
        );

        return builder;
    }

    private static EndpointFilterDelegate CreateValidationFilterFactory(
        ValidationFilterOptions options,
        IList<object> metadata,
        EndpointFilterFactoryContext context,
        EndpointFilterDelegate next)
    {
        IEnumerable<ValidateableParameterDescriptor> validateableParameters
            = GetValidateableParameters(options, context.MethodInfo, metadata);

        if (validateableParameters.Any())
        {
            // Caches the parameters to avoid reflecting each time
            return invocationContext => CreateValidationFilter(options, validateableParameters, invocationContext, next);
        }

        // pass-thru
        return invocationContext => next(invocationContext);
    }

    private static async ValueTask<object?> CreateValidationFilter(
        ValidationFilterOptions options,
        IEnumerable<ValidateableParameterDescriptor> validationDescriptors,
        EndpointFilterInvocationContext invocationContext,
        EndpointFilterDelegate next)
    {
        foreach (ValidateableParameterDescriptor descriptor in validationDescriptors)
        {
            var argument = invocationContext.Arguments[descriptor.ArgumentIndex];

            // Resolve the validator
            IValidator? validator
                = invocationContext.HttpContext.RequestServices.GetService(descriptor.ValidatorType) as IValidator;

            // TODO consider whether we mutate the descriptor to skip if no validator is registered

            if (argument is not null && validator is not null)
            {
                var validationResult = await validator.ValidateAsync(
                    new ValidationContext<object>(argument)
                );

                if (!validationResult.IsValid)
                {
                    return options.InvalidResultFactory(validationResult, descriptor.Parameter);
                }
            }
        }

        return await next.Invoke(invocationContext);
    }

    private static IEnumerable<ValidateableParameterDescriptor> GetValidateableParameters(
        ValidationFilterOptions options,
        MethodInfo methodInfo,
        IList<object> metadata)
    {
        ParameterInfo[] parameters = methodInfo.GetParameters();

        for (int i = 0; i < parameters.Length; i++)
        {
            ParameterInfo parameter = parameters[i];

            if (options.ShouldValidate(parameter, metadata))
            {
                Type validatorType = typeof(IValidator<>).MakeGenericType(parameter.ParameterType);

                yield return new ValidateableParameterDescriptor
                {
                    ArgumentIndex = i,
                    ArgumentType = parameter.ParameterType,
                    ValidatorType = validatorType,
                    Parameter = parameter
                };
            }
        }
    }

    private class ValidateableParameterDescriptor
    {
        public required int ArgumentIndex { get; init; }
        public required Type ArgumentType { get; init; }
        public required Type ValidatorType { get; init; }
        public required ParameterInfo Parameter { get; init; }
    }
}