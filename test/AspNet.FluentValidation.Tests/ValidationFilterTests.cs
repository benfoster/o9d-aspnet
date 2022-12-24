using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace O9d.AspNet.FluentValidation.Tests;

public class ValidationFilterTests
{
    [Fact]
    public async void Skips_validation_without_filter()
    {
        using var app = await CreateApplication(app
            => app.MapPost("/things", (DoSomething _) => Results.Ok()));

        using var httpResponse
            = await app.GetTestClient().PostAsJsonAsync("/things", new DoSomething());

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
    }

    [Fact]
    public async void Validates_using_attribute_strategy()
    {
        using var app = await CreateApplication(
            app =>
            {
                var group = app.MapGroup("/")
                    .WithValidationFilter(); // Uses attribute strategy by default

                group.MapPost("/things", ([Validate] DoSomething _) => Results.Ok());
            },
            services => services.AddScoped<IValidator<DoSomething>, DoSomething.Validator>()
        );

        using var httpResponse
            = await app.GetTestClient().PostAsJsonAsync("/things", new DoSomething());

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Skips_validation_if_validator_not_registered()
    {
        using var app = await CreateApplication(
            app =>
            {
                var group = app.MapGroup("/")
                    .WithValidationFilter();

                group.MapPost("/things", ([Validate] DoSomething _) => Results.Ok());
            }
        );

        using var httpResponse
            = await app.GetTestClient().PostAsJsonAsync("/things", new DoSomething());

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Validates_with_metadata_strategy()
    {
        using var app = await CreateApplication(
            app =>
            {
                var group = app.MapGroup("/")
                    .WithValidationFilter(options => options.ShouldValidate = ValidationStrategies.HasValidationMetadata)
                    .RequireValidation(typeof(DoSomething));

                group.MapPost("/things", (DoSomething _) => Results.Ok());
            },
            services => services.AddScoped<IValidator<DoSomething>, DoSomething.Validator>()
        );

        using var httpResponse
            = await app.GetTestClient().PostAsJsonAsync("/things", new DoSomething());

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Skips_metadata_validation_when_type_not_registered()
    {
        using var app = await CreateApplication(
            app =>
            {
                var group = app.MapGroup("/")
                    .WithValidationFilter(options => options.ShouldValidate = ValidationStrategies.HasValidationMetadata)
                    .RequireValidation(Array.Empty<Type>());

                group.MapPost("/things", (DoSomething _) => Results.Ok());
            },
            services => services.AddScoped<IValidator<DoSomething>, DoSomething.Validator>()
        );

        using var httpResponse
            = await app.GetTestClient().PostAsJsonAsync("/things", new DoSomething());

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Validates_with_type_strategy()
    {
        using var app = await CreateApplication(
            app =>
            {
                var group = app.MapGroup("/")
                    .WithValidationFilter(options => options.ShouldValidate = ValidationStrategies.TypeImplements<IValidateable>());

                group.MapPost("/things", (DoSomething _) => Results.Ok());
            },
            services => services.AddScoped<IValidator<DoSomething>, DoSomething.Validator>()
        );

        using var httpResponse
            = await app.GetTestClient().PostAsJsonAsync("/things", new DoSomething());

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Can_override_validation_result()
    {
        using var app = await CreateApplication(
            app =>
            {
                var group = app.MapGroup("/")
                    .WithValidationFilter(options => options.InvalidResultFactory = _ => Results.BadRequest());

                group.MapPost("/things", ([Validate] DoSomething _) => Results.Ok());
            },
            services => services.AddScoped<IValidator<DoSomething>, DoSomething.Validator>()
        );

        using var httpResponse
            = await app.GetTestClient().PostAsJsonAsync("/things", new DoSomething());

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async void Validates_when_using_class_attribute()
    {
        using var app = await CreateApplication(
            app =>
            {
                var group = app.MapGroup("/")
                    .WithValidationFilter();

                group.MapPost("/things", (AttributedThing _) => Results.Ok());
            },
            services => services.AddScoped<IValidator<AttributedThing>, AttributedThing.Validator>()
        );

        using var httpResponse
            = await app.GetTestClient().PostAsJsonAsync("/things", new AttributedThing());

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async void Validates_using_runtime_type()
    {
        using var app = await CreateApplication(
            app =>
            {
                var group = app.MapGroup("/")
                    .WithValidationFilter();

                group.MapPost("/people", ([Validate] Person _) => Results.Ok());
            },
            services =>
            {
                services.AddScoped<IValidator<Person>, Person.PersonValidator>();
                services.AddScoped<IValidator<Student>, Student.StudentValidator>();
            }
        );

        static async Task Test(WebApplication app, object request, HttpStatusCode expected)
        {
            using var httpResponse
                = await app.GetTestClient().PostAsJsonAsync("/people", request);

            httpResponse.StatusCode.ShouldBe(expected);
        }

        await Test(app, new { }, HttpStatusCode.UnprocessableEntity); // Validates with base validator
        await Test(app, new { Name = "J Doe" }, HttpStatusCode.OK); // Validates with base validator
        await Test(app, new { Type = "student", Name = "J Doe" }, HttpStatusCode.UnprocessableEntity); // Validates with derived validator
        await Test(app, new { Type = "student", Name = "J Doe", StudentId = "123abc" }, HttpStatusCode.OK); // Validates with derived validator
    }

    private static async Task<WebApplication> CreateApplication(Action<WebApplication> configureApplication, Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder();
        configureServices?.Invoke(builder.Services);
        builder.WebHost.UseTestServer();

        var app = builder.Build();

        configureApplication(app);

        await app.StartAsync();
        return app;
    }

    public class DoSomething : IValidateable
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

    [Validate]
    public class AttributedThing : IValidateable
    {
        public string? Name { get; set; }

        public class Validator : AbstractValidator<AttributedThing>
        {
            public Validator()
            {
                RuleFor(x => x.Name).NotEmpty();
            }
        }
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
    [JsonDerivedType(typeof(Student), "student")]
    public class Person
    {
        public string? Name { get; set; }
        public class PersonValidator : AbstractValidator<Person>
        {
            public PersonValidator() => RuleFor(x => x.Name).NotEmpty();
        }
    }

    public class Student : Person
    {
        public string? StudentId { get; set; }
        public class StudentValidator : AbstractValidator<Student>
        {
            public StudentValidator()
            {
                RuleFor(x => x.Name).NotEmpty();
                RuleFor(x => x.StudentId).NotEmpty();
            }
        }
    }

    public interface IValidateable { }
}