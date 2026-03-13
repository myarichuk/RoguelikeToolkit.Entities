using System;
using RoguelikeToolkit.Entities.Exceptions;
using Xunit;

namespace RoguelikeToolkit.Entities.Tests;

public class ExceptionsTests
{
    [Fact]
    public void ComponentTypeConflictException_Should_Have_Correct_Message()
    {
        var exception = new ComponentTypeConflictException("MyComponent", "MyAssembly.MyComponent");
        Assert.Contains("MyComponent", exception.Message);
    }

    [Fact]
    public void TemplateAlreadyExistsException_Should_Have_Correct_Message()
    {
        var exception = new TemplateAlreadyExistsException("MyTemplate");
        Assert.Contains("MyTemplate", exception.Message);
    }
}