# ASP.NET Extensions

Opinionated extensions for ASP.NET Core.

## O9d.AspNet.FluentValidation

This package includes a validation filter that can be used with ASP.NET Minimal APIs to automatically validate incoming requests using [FluentValidation](https://github.com/FluentValidation/FluentValidation).

For more information on the motivation behind this filter, see [this blog post](https://benfoster.io/blog/minimal-api-validation-endpoint-filters/).

### Installation

Install from [Nuget](https://www.nuget.org/packages/O9d.AspNet.FluentValidation).

```
dotnet add package O9d.AspNet.FluentValidation
```

### Usage

In your `Program.cs` register the validators as singleton.
```c#
builder.Services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Singleton);
```

Rather than attaching the filter to every endpoint, create a group under which your endpoints are created and add the validation filter:

```c#
var group = app.MapGroup("/")
    .WithValidationFilter();
```

Automatic validation is opt-in and defaults to a strategy that uses the provided `[Validate]` attribute:

```c#
group.MapPost("/things", ([Validate] DoSomething _) => Results.Ok());

public class DoSomething
{
    public string? Name { get; set; }

    public class Validator : AbstractValidator<DoSomething>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }
}
```

If the request parameter is not valid, by default, a `ValidationProblem` is returned with the HTTP status code `422 - Unprocessable Entity` 

```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "One or more validation errors occurred.",
  "status": 422,
  "errors": {
    "Name": ["'Name' must not be empty."]
  }
}
```

### Validation Strategies

A validation strategy determines how whether a parameter type is validateable. The default behaviour is to use the `[Validate]` attribute which can be applied to the parameter (as above) or directly to the class. The library also includes support for the following:

#### Metadata-based strategy

You can choose to decorate the endpoints you wish to validate explicitly using metadata:

```c#
var group = app.MapGroup("/")
    .WithValidationFilter(options => options.ShouldValidate = ValidationStrategies.HasValidationMetadata)
    .RequireValidation(typeof(DoSomething));

group.MapPost("/things", (DoSomething _) => Results.Ok());
```

You need to specify the types that should be enlisted for validation so it generally only makes sense to do this at a group level.

#### Type convention driven strategy

My preferred approach is to define a marker interface e.g. `IValidateable` and then decorate my input parameters with this:

```c#
var group = app.MapGroup("/")
    .WithValidationFilter(options => options.ShouldValidate = ValidationStrategies.TypeImplements<IValidateable>());

group.MapPost("/things", (DoSomething _) => Results.Ok());
```

#### Custom strategy

If none of the built-in strategies work for you, you can create your own implementation of the `ValidationStrategy` delegate:

```c#
public delegate bool ValidationStrategy(ParameterInfo parameterInfo, IList<object> endpointMetadata);
```

### Overriding the validation result

You can override the default validation result by providing your own factory, which takes the FluentValidation `ValidationResult` as an input:

```c#
var group = app.MapGroup("/")
    .WithValidationFilter(options => options.InvalidResultFactory = validationResult => Results.BadRequest());

group.MapPost("/things", ([Validate] DoSomething _) => Results.Ok());
```
