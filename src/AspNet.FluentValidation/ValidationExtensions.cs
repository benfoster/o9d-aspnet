using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using O9d.AspNet.FluentValidation;
using System.Text.Json.Serialization;

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

            Type validatorType = descriptor.ValidatorType;

            // If a type map is provided then attempt to resolve a validator using the runtime type
            if (descriptor.UseRuntimeType
                && argument?.GetType() is Type runtimeType
                && descriptor.TypeMap!.TryGetValue(runtimeType, out Type? runtimeValidatorType))
            {
                validatorType = runtimeValidatorType;
            }

            // Resolve the validator
            IValidator? validator
                = invocationContext.HttpContext.RequestServices.GetService(validatorType) as IValidator;

            // TODO consider whether we mutate the descriptor to skip if no validator is registered

            if (argument is not null && validator is not null)
            {
                var validationResult = await validator.ValidateAsync(
                    new ValidationContext<object>(argument)
                );

                if (!validationResult.IsValid)
                {
                    return options.InvalidResultFactory(validationResult);
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

        static Type CreateValidatorType(Type parameterType)
            => typeof(IValidator<>).MakeGenericType(parameterType);

        for (int i = 0; i < parameters.Length; i++)
        {
            ParameterInfo parameter = parameters[i];

            if (options.ShouldValidate(parameter, metadata))
            {
                var descriptor = new ValidateableParameterDescriptor
                {
                    ArgumentIndex = i,
                    ArgumentType = parameter.ParameterType,
                    ValidatorType = CreateValidatorType(parameter.ParameterType)
                };

                // If the ValidationStrategy delegate changes to return the validateable types
                // then support for System.Text.Json polymorphic deserialization then
                // this could just be made into a strategy
                if (IsPolymorphicType(parameter.ParameterType, out Type[] derivedTypes))
                {
                    // Create the validator types upfront for each derived type
                    descriptor.TypeMap = derivedTypes.ToDictionary(t => t, t => CreateValidatorType(t));
                }

                yield return descriptor;
            }
        }
    }

    private static bool IsPolymorphicType(Type parameterType, out Type[] derivedTypes)
    {
        derivedTypes = parameterType.GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(attr => attr.DerivedType)
            .ToArray();

        return derivedTypes.Length > 0;
    }

    private class ValidateableParameterDescriptor
    {
        public required int ArgumentIndex { get; init; }
        public required Type ArgumentType { get; init; }
        public required Type ValidatorType { get; init; }
        public Dictionary<Type, Type>? TypeMap { get; set; }

        public bool UseRuntimeType => TypeMap?.Count > 0;
    }
}