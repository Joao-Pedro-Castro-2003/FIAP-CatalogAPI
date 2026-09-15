using CatalogAPI.Controllers;
using CatalogAPI.Reviews;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
namespace CatalogAPI.Tests;
public class RequestValidationTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    public void MvcValidatesRequestRecordsWithoutMetadataExceptions(string culture)
    {
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        try {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(culture);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddControllers();
            using var provider = services.BuildServiceProvider();
            var validator = provider.GetRequiredService<IObjectModelValidator>();
            (object request, bool valid)[] cases = [
                (new GameRequest("Game", 0.01m), true), (new GameRequest("", -1m), false), (new GameRequest("Free", 0m), false),
                (new PromotionRequest(1, 101, DateTime.UtcNow, DateTime.UtcNow.AddDays(1), true), false),
                (new ReviewRequest(5, "Good", []), true), (new ReviewRequest(0, "", []), false)
            ];
            foreach (var (request, valid) in cases) {
                var context = new ActionContext(new DefaultHttpContext { RequestServices = provider }, new RouteData(), new ActionDescriptor());
                validator.Validate(context, null, "", request);
                Assert.Equal(valid, context.ModelState.IsValid);
            }
        } finally { System.Globalization.CultureInfo.CurrentCulture = previousCulture; }
    }
}
