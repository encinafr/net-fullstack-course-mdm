﻿using AlfaBank.Core.Models;
using AlfaBank.Services;
using AlfaBank.WebApi.HealthCheckers;
using AlfaBank.WebApi.Middleware;
using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core.Enrichers;
using Serilog.Enrichers.AspNetCore.HttpContext;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using AlfaBank.WebApi.HostedServices;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

// ReSharper disable UnusedMember.Global

#pragma warning disable 1591

namespace AlfaBank.WebApi
{
    [ExcludeFromCodeCoverage]
    public class Startup
    {
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        /// <param name="configuration">The current configuration.</param>
        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Configures services for the application.
        /// </summary>
        /// <param name="services">The collection of services to configure the application with.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAutoMapper(options => options.AddProfile<DomainToDtoProfile>());

            // Add owns services
            services
                .AddInMemoryUserStorage()
                .AddAlfaBankServices();

            // Add Background service
            services.AddHostedService<TariffHostedService>();

            // Add MVC and other services
            services.AddMvc(options => options.EnableEndpointRouting = true)
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            services.AddApiVersioning(
                options =>
                {
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                    // reporting api versions will return the headers "api-supported-versions" and "api-deprecated-versions"
                    options.ReportApiVersions = true;
                });

            services.AddVersionedApiExplorer(
                options =>
                {
                    // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
                    // note: the specified format code will format the version as "'v'major[.minor][-status]"
                    options.GroupNameFormat = "'v'VVV";

                    // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
                    // can also be used to control the format of the API version in route templates
                    options.SubstituteApiVersionInUrl = true;
                });

            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

            services.AddSwaggerGen(
                options =>
                {
                    // add a custom operation filter which sets default values
                    options.OperationFilter<SwaggerDefaultValues>();

                    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                    // integrate xml comments
                    options.IncludeXmlComments(xmlPath);
                });

            // Add custom HealthCheck and UI
            services
                .AddCustomHealthChecks()
                .AddElkHealthChecks(_configuration);
        }

        /// <summary>
        /// Configures the application using the provided builder, hosting environment, and API version description provider.
        /// </summary>
        /// <param name="app">The current application builder.</param>
        /// <param name="env">The current hosting environment.</param>
        /// <param name="provider">The API version descriptor provider used to enumerate defined API versions.</param>
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApiVersionDescriptionProvider provider)
        {
            app.UseSerilogLogContext(options =>
            {
                options.EnrichersForContextFactory = context => new[]
                {
                    // TraceIdentifier property will be available in all chained middlewares. And yes - it is HttpContext specific
                    new PropertyEnricher("TraceIdentifier", context.TraceIdentifier)
                };
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            app.UseHttpStatusCodeExceptionMiddleware();

            app.UseSwagger();

            app.UseSwaggerUI(
                options =>
                {
                    // build a swagger endpoint for each discovered API version
                    foreach (var description in provider.ApiVersionDescriptions)
                    {
                        options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
                            description.GroupName.ToUpperInvariant());
                    }
                });

            app.UseCustomHealthCheckEndpoints();

            app.UseMvc();
        }
    }
}