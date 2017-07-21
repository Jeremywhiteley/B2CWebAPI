﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace B2CWebApi
{
    public class Startup
    {
        public static string ScopeRead;
        public static string ScopeWrite;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:B2CWebApi.Startup"/> class.
        /// </summary>
        /// <param name="env">Env.</param>
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsDevelopment())
            {
                // For more details on using the user secret store see https://go.microsoft.com/fwlink/?LinkID=532709
                builder.AddUserSecrets<Startup>();
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

		/// <summary>
		///  This method gets called by the runtime. Use this method to add services to the container.
		/// </summary>
		/// <param name="services">Services.</param>

		public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();
        }

		/// <summary>
		/// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		/// </summary>
		/// <returns>The configure.</returns>
		/// <param name="app">App.</param>
		/// <param name="env">Env.</param>
		/// <param name="loggerFactory">Logger factory.</param>

		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseJwtBearerAuthentication(new JwtBearerOptions
            {
                Authority = string.Format("https://login.microsoftonline.com/tfp/{0}/{1}/v2.0/", 
                    Configuration["Authentication:AzureAd:Tenant"], Configuration["Authentication:AzureAd:Policy"]),
                Audience = Configuration["Authentication:AzureAd:ClientId"],
				AutomaticAuthenticate = true,
				AutomaticChallenge = true,
                Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = AuthenticationFailed
                },
				TokenValidationParameters = new TokenValidationParameters
				{
					//The audience must match
					ValidateAudience = true,
					ValidAudience = Configuration["Authentication:AzureAd:ClientId"],

					//Token must still be valid
					ValidateLifetime = true,

					// The signing key must match
					ValidateIssuerSigningKey = true,
				}            
            });

            ScopeRead = Configuration["Authentication:AzureAd:ScopeRead"];
            ScopeWrite = Configuration["Authentication:AzureAd:ScopeWrite"];

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
        /// <summary>
        /// Authentications the failed.
        /// </summary>
        /// <returns>The failed.</returns>
        /// <param name="arg">Argument.</param>
        private Task AuthenticationFailed(AuthenticationFailedContext arg)
        {
            // For debugging purposes only!
            var s = $"AuthenticationFailed: {arg.Exception.Message}";
            arg.Response.ContentLength = s.Length;
            arg.Response.Body.Write(Encoding.UTF8.GetBytes(s), 0, s.Length);
            return Task.FromResult(0);
        }
    }
}
