using AutoMapper;
using Digipolis.ApplicationServices;
using Digipolis.Correlation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using StarterKit.Shared.Extensions;
using StarterKit.Shared.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Digipolis.Swagger.Startup;

//--dataaccess-startupImports--

namespace StarterKit.Startup
{
  public class Startup
  {
    public Startup(IConfiguration configuration, IHostEnvironment env)
    {
      Configuration = configuration;
      Environment = env;
    }

    public IConfiguration Configuration { get; }
    public IHostEnvironment Environment { get; }

    public virtual void ConfigureServices(IServiceCollection services)
    {
      #region Read settings

      // Check out ExampleController to find out how these configs are injected into other classes
      AppSettings.RegisterConfiguration(services, Configuration.GetSection(Shared.Constants.ConfigurationSectionKey.AppSettings), Environment);
      //--dataaccess-registerConfiguration--

      AppSettings appSettings;
      //--dataaccess-variable--

      using (var provider = services.BuildServiceProvider())
      {
        appSettings = provider.GetService<IOptions<AppSettings>>().Value;
        //--dataaccess-getService--
      }

      #endregion

      #region Add Correlation and application services

      services.AddCorrelation(options =>
      {
        options.CorrelationHeaderRequired = !Environment.IsDevelopment();
      });

      services.AddApplicationServices(opt =>
      {
        opt.ApplicationId = appSettings.ApplicationId;
        opt.ApplicationName = appSettings.AppName;
      });

      #endregion

      #region Logging

      services.AddLoggingEngine();

      #endregion

      //--dataaccess-startupServices--

      #region Add routing and versioning

      services
        .AddRouting(options =>
        {
          options.LowercaseUrls = true;
          options.LowercaseQueryStrings = true;
        })
        .AddControllers()
        .AddNewtonsoftJson(options =>
        {
          options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
          options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
          options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
          options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
          options.SerializerSettings.MetadataPropertyHandling = MetadataPropertyHandling.Ignore;
          options.SerializerSettings.Converters.Add(new StringEnumConverter());
        });

      services
        .AddApiVersioning(options =>
        {
          options.ReportApiVersions = true;
          options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
          options.AssumeDefaultVersionWhenUnspecified = true;
          options.UseApiBehavior = false;
        })
        .AddVersionedApiExplorer(options =>
        {
          options.GroupNameFormat = "'v'VVV";
          options.SubstituteApiVersionInUrl = true;
        });

      #endregion

      #region DI and Automapper

      services.AddBusinessServices();
      services.AddServiceAgentServices();
      services.AddDataAccessServices();

      services.AddAutoMapper(typeof(Startup).Assembly);

      #endregion

      #region Swagger

      services
        .AddDigipolisSwagger(options =>
        {
          // Define multiple swagger docs if you have multiple api versions
          options.SwaggerDoc("v1",
            new OpenApiInfo
            {
              Title = "STARTERKIT API",
              Version = "v1",
              Description = "<API DESCRIPTION>",
              TermsOfService = null,
              Contact = new OpenApiContact()
              {
                Email = "<MAIL>",
                Name = "<NAME>"
              }
            });
        });

      #endregion

      #region Global error handling

      services.AddGlobalErrorHandling<ApiExceptionMapper>();

      #endregion
    }

    public void Configure(IApplicationBuilder app,
                          IApiVersionDescriptionProvider versionProvider,
                          ILoggerFactory loggerFactory, IHostApplicationLifetime appLifetime)
    {
      loggerFactory.AddLoggingEngine(app, appLifetime, Configuration);

      // Enable Serilog selflogging to console.
      Serilog.Debugging.SelfLog.Enable(Console.Out);

      app.UseApiExtensions();

      // CORS
      app.UseCors((policy) =>
      {
        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
        policy.AllowAnyOrigin();
      });

      var rewriteOptions = new RewriteOptions();
      rewriteOptions.AddRedirect("^$", "swagger");
      app.UseRewriter(rewriteOptions);

      app.UseAuthentication();
      app.UseRouting();

      app.UseAuthorization();
      app.UseEndpoints(endpoints =>
        endpoints.MapDefaultControllerRoute().RequireAuthorization("IsAuthenticated"));

      app.UseDigipolisSwagger();
      // app.UseSwagger(options =>
      // {
      //   options.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
      //   {
      //     swaggerDoc.Servers = new List<OpenApiServer>() { new OpenApiServer() { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}" } };
      //   });
      // });
      //
      // app.UseSwaggerUI(options =>
      // {
      //   foreach (var description in versionProvider.ApiVersionDescriptions)
      //   {
      //     options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
      //     options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
      //   }
      // });
    }
  }
}
