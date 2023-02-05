using System.Net;
using System.Reflection;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace O9d.AspNet.FluentValidation;

/// <summary>
/// Options used to configure the Validation Filter
/// </summary>
public sealed class ValidationFilterOptions
{
    /// <summary>
    /// Gets or sets the delegate used to determine whether the endpoint parameter is validateable
    /// </summary>
    public ValidationStrategy ShouldValidate { get; set; } = ValidationStrategies.HasValidateAttribute;

    /// <summary>
    /// Gets or sets the factory used to create a HTTP result when validation fails. Defaults to a HTTP 422 Validation Problem.
    /// </summary>
    public Func<ValidationResult, ParameterInfo, IResult> InvalidResultFactory { get; set; } = CreateValidationProblemResult;

    private static IResult CreateValidationProblemResult(ValidationResult validationResult, ParameterInfo parameter)
        => Results.ValidationProblem(validationResult.ToDictionary(), statusCode: (int)HttpStatusCode.UnprocessableEntity);
}