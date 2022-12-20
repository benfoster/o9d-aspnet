namespace O9d.AspNet.FluentValidation;

/// <summary>
/// Attribute used to indicate an input parameter or class should be validated
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
public sealed class ValidateAttribute : Attribute
{
}