using GodotGenerator.Infrastructure.Ai.Options;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Xunit;

namespace GodotGenerator.Infrastructure.Ai.Tests;

/// <summary>
/// Validates data-annotation constraints for infrastructure option models.
/// </summary>
public sealed class OptionsValidationTests
{
    /// <summary>
    /// Ensures <see cref="LlmOptions"/> fails validation when the API key is missing.
    /// </summary>
    [Fact]
    public void LlmOptions_without_api_key_is_invalid()
    {
        var options = new LlmOptions
        {
            ChatModelId = "gpt-4o-mini",
            ApiKey = string.Empty,
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(LlmOptions.ApiKey)));
    }

    /// <summary>
    /// Ensures <see cref="OrchestrationOptions"/> fails validation when the generic failure message is missing.
    /// </summary>
    [Fact]
    public void OrchestrationOptions_without_failure_message_is_invalid()
    {
        var options = new OrchestrationOptions
        {
            GenericFailureMessage = string.Empty,
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(OrchestrationOptions.GenericFailureMessage)));
    }
}
