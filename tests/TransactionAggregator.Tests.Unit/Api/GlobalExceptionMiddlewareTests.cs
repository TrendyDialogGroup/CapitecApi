using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregator.Api.Middleware;
using TransactionAggregator.Application.Common.Models;
using TransactionAggregator.Domain.Exceptions;

namespace TransactionAggregator.Tests.Unit.Api;

public sealed class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenValidationException_ShouldReturnBadRequest()
    {
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new ValidationException(["Page must be greater than 0."]),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var payload = await JsonSerializer.DeserializeAsync<ApiResponse<object>>(
            context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        payload!.Success.Should().BeFalse();
        payload.Errors.Should().ContainSingle();
    }

    [Fact]
    public async Task InvokeAsync_WhenDuplicate_ShouldReturnConflict()
    {
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new DuplicateTransactionException("EXT-1"),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandled_ShouldReturnInternalServerErrorWithoutLeak()
    {
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new InvalidOperationException("secret internals"),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.Should().NotContain("secret internals");
        body.Should().Contain("unexpected error");
    }
}
