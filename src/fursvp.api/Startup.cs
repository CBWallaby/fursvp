// <copyright file="Startup.cs" company="skippyfox">
// Copyright (c) skippyfox. All rights reserved.
// Licensed under the MIT license. See the license.md file in the project root for full license information.
// </copyright>

namespace Fursvp.Api
{
    using System.Text;
    using Fursvp.Api.Filters;
    using Fursvp.Data;
    using Fursvp.Data.Firestore;
    using Fursvp.Domain;
    using Fursvp.Domain.Authorization;
    using Fursvp.Domain.Forms;
    using Fursvp.Domain.Validation;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.AspNetCore.Mvc.Routing;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.IdentityModel.Tokens;

    /// <summary>
    /// Fursvp .NET Core Web Api initialization routines.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        /// <param name="configuration">An instance of <see cref="IConfiguration"/>.</param>
        /// <param name="environment">The <see cref="IWebHostEnvironment"/> instance containing environment specific settings.</param>
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            this.Configuration = configuration;
            this.Environment = environment;
        }

        private IConfiguration Configuration { get; }

        private IWebHostEnvironment Environment { get; }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">The container in which components are registered.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
            services.AddControllers();
            services.AddSingleton<IEventService, EventService>();
            this.ConfigureRepositoryServices(services);
            services.AddSingleton<IValidateEmail, ValidateEmail>();
            services.AddSingleton<IProvideDateTime, UtcDateTimeProvider>();
            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();
            services.AddScoped<IUrlHelper>(x => x.GetRequiredService<IUrlHelperFactory>().GetUrlHelper(x.GetService<IActionContextAccessor>().ActionContext));
            services.AddSingleton<DebugModeOnlyFilter>();
            services.AddLogging(lc =>
            {
                lc.ClearProviders();
                lc.AddConsole();
            });
            services.AddMvc(options =>
            {
                options.Filters.Add(typeof(ApiExceptionFilter));
            });

            var key = Encoding.ASCII.GetBytes("Some secret goes here!"); // TODO

            services.AddHttpContextAccessor(); // For authorization / access to user info.
            services.AddSingleton<IUserAccessor, ClaimsPrincipalUserAccessor>();
            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(x =>
            {
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                };
            });
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> instance to configure.</param>
        /// <param name="env">The <see cref="IWebHostEnvironment"/> instance containing environment specific settings.</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors(x => x
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());

            app.UseAuthentication();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private void ConfigureRepositoryServices(IServiceCollection services)
        {
            services.AddSingleton<IRepository<Event>>(s =>
            {
                //// var baseEventRepository = new InMemoryEventRepository(new FormPromptFactory());
                var baseEventRepository = new FirestoreRepository<Event>(new EventMapper(new FormPromptFactory()));
                var dateTimeProvider = s.GetRequiredService<IProvideDateTime>();
                var validateEmail = s.GetRequiredService<IValidateEmail>();
                var validateMember = new ValidateMember(validateEmail);
                var validateTimeZone = new ValidateTimeZone();
                var validateEvent = new ValidateEvent(dateTimeProvider, validateMember, validateTimeZone);

                var userAccessor = s.GetRequiredService<IUserAccessor>();

                var eventService = s.GetRequiredService<IEventService>();
                var authorizeEvent = new AuthorizeEvent(
                    new AuthorizeMemberAsAuthor(),
                    new AuthorizeMemberAsOrganizer(userAccessor),
                    new AuthorizeMemberAsAttendee(userAccessor),
                    new AuthorizeFrozenMemberAsAttendee(),
                    eventService,
                    userAccessor);

                var validateEventRepository = new RepositoryWithValidation<Event>(baseEventRepository, validateEvent);

                return new RepositoryWithAuthorization<Event>(validateEventRepository, authorizeEvent);
            });
        }
    }
}
