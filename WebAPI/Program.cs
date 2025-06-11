using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

namespace WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

			builder.Configuration.AddJsonFile("secrets.json", true);

			Log.Logger = new LoggerConfiguration()
	            .ReadFrom.Configuration(builder.Configuration)
	            .Enrich.FromLogContext()
	            .WriteTo.Console()
	            .CreateLogger();

			builder.Host.UseSerilog();

			builder.Services.AddCors(options =>
			{
				options.AddPolicy("AllowAllOrigins",
					policy =>
					{
						policy.AllowAnyOrigin()
							  .AllowAnyHeader()
							  .AllowAnyMethod();
					});
			});

			IConfigurationSection jwtSettings = builder.Configuration.GetSection("JwtSettings");
			builder.Services.AddAuthentication(options =>
			{
				options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
				options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
			})
			.AddJwtBearer(options =>
			{
				options.TokenValidationParameters = new TokenValidationParameters
				{
					ValidateIssuer = true,
					ValidateAudience = true,
					ValidateLifetime = true,
					ValidateIssuerSigningKey = true,
					ValidIssuer = jwtSettings["Issuer"],
					ValidAudience = jwtSettings["Audience"],
					IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]))
				};
			});

			builder.Services.AddAuthorization();
			builder.Services.AddControllers(options =>
			{
				AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
					.RequireAuthenticatedUser()
					.Build();

				options.Filters.Add(new AuthorizeFilter(policy));
			});

            WebApplication app = builder.Build();

			app.UseCors("AllowAllOrigins");

			app.UseAuthentication();
			app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
