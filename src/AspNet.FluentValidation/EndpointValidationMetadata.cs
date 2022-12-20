namespace O9d.AspNet.FluentValidation;

/// <summary>
/// Marker metadata used to indicate that the endpoint input parameter should be validated
/// </summary>
public class EndpointValidationMetadata
{
    public EndpointValidationMetadata(Type[] typesToValidate)
    {
        if (typesToValidate is null)
        {
            throw new ArgumentNullException(nameof(typesToValidate));
        }

        TypesToValidate = typesToValidate;
    }

    public Type[] TypesToValidate { get; }
}