using Carter;
using Carter.ModelBinding;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Server.Contracts;
using Server.Data;
using Server.Domain.Entities;
using Server.Security.Interfaces;

namespace Server.Features;

public class Login : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (LoginRequest request, ISender sender) =>
            {
                var command = new Command(request.Email, request.Password);
                return await sender.Send(command);
            })
            .WithTags("Authentication")
            .Produces<LoginResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status422UnprocessableEntity);
    }
    
    public sealed record Command(
        string Email,
        string Password
    ) : IRequest<IResult>;
    
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(request => request.Email)
                .NotEmpty()
                .EmailAddress();

            RuleFor(request => request.Password)
                .NotEmpty();
        }
    }
    
    internal sealed class Handler(
        IValidator<Command> validator,
        UserManager<User> userManager,
        ISecurityService securityService,
        AppDbContext dbContext
    ) : IRequestHandler<Command, IResult>
    {
        public async Task<IResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
                return Results.UnprocessableEntity(validationResult.GetValidationProblems());

            var user = await dbContext
                .Users
                .Include(u => u.RefreshTokens)
                .Where(u => u.NormalizedEmail == request.Email.Trim().ToUpper())
                .FirstOrDefaultAsync(cancellationToken);
            
            if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
                return Results.BadRequest("login or password is incorrect");
            
            securityService.UpdateRefreshTokenCount(user);
            var accessToken = securityService.GenerateAccessToken(user);
            var refreshToken = securityService.GenerateRefreshToken(user);
            
            user.RefreshTokens.Add(refreshToken);
            dbContext.Users.Update(user);
            await dbContext.SaveChangesAsync(cancellationToken);
            
            return Results.Ok(
                new LoginResponse(accessToken, refreshToken.Token, refreshToken.Expires));
        }
    }
}

