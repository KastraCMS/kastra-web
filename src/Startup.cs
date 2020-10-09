using System;
using System.Reflection;
using System.Threading.Tasks;
using Kastra.Core;
using Kastra.Core.Business;
using Kastra.Core.Configuration;
using Kastra.Core.Constants;
using Kastra.Core.Dto;
using Kastra.Core.Modules;
using Kastra.Core.Services;
using Kastra.Web.Identity;
using Kastra.Web.Middlewares;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Environments = Microsoft.Extensions.Hosting.Environments;

namespace Kastra.Web
{
    public class Startup
    {
        private bool _isInstalled = false;

        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Add options
            services.AddOptions();

            AppSettings appSettings = Configuration.GetSection("AppSettings").Get<AppSettings>();
            services.Configure<AppSettings>(Configuration);

            #region Check database

            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            _isInstalled = !String.IsNullOrEmpty(connectionString) && HasTables(connectionString);

            #endregion

            // Logging
            services.AddLogging(config =>
            {
                // clear out default configuration
                config.ClearProviders();

                config.AddConfiguration(Configuration.GetSection("Logging"));
                config.AddDebug();
                config.AddEventSourceLogger();

                // Add console only for development
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == Environments.Development)
                {
                    config.AddConsole();
                }
            });

            if (!String.IsNullOrEmpty(connectionString))
            {
                // Add framework services.
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

                services.AddIdentity<ApplicationUser, ApplicationRole>()
                    .AddEntityFrameworkStores<ApplicationDbContext>()
                    .AddDefaultTokenProviders();

                services.ConfigureApplicationCookie(options =>
                {
                    if (appSettings.Configuration.DevelopmentMode)
                    {
                        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
                        options.Cookie.SameSite = SameSiteMode.None;
                    }
                    options.Events =
                        new CookieAuthenticationEvents
                        {
                            OnRedirectToLogin = ctx =>
                            {
                                if (ctx.Request.Path.StartsWithSegments("/api") && ctx.Response.StatusCode == 200)
                                {
                                    ctx.Response.StatusCode = 401;
                                    return Task.FromResult<object>(null);
                                }

                                ctx.Response.Redirect(ctx.RedirectUri);
                                return Task.FromResult<object>(null);
                            }
                        };
                });
            }

            // Add dependencies
            DirectoryAssemblyLoader.LoadAllAssemblies(appSettings);
            int assembliesLength = KastraAssembliesContext.Instance.Assemblies.Count;

            if (assembliesLength > 0 && !String.IsNullOrEmpty(connectionString))
            {
                Assembly[] assemblies = new Assembly[assembliesLength];
                KastraAssembliesContext.Instance.Assemblies.Values.CopyTo(assemblies, 0);

                services.AddDependencyInjection(Configuration, assemblies);
            }

            services.AddKastraServices();

            // Add caching support
            services.AddMemoryCache();

            // Configure Kastra options with site configuration
            if (_isInstalled)
            {
                services.ConfigureKastraOptions();
            }

            // Localization
            services.AddLocalization(options =>
            {
                options.ResourcesPath = "Resources";
            });
            services.AddTransient(typeof(IStringLocalizer<>), typeof(JsonStringLocalizer<>));

            // Add module view engine
            services.TryAddSingleton<IRazorViewEngine, ModuleViewEngine>();

            // Add Mvc
            var mvcBuilder = services.AddControllersWithViews().AddRazorRuntimeCompilation();
            mvcBuilder.AddKastraModule();
            mvcBuilder.AddApplicationParts();

            // Add authorizations
            services.AddAuthorization(options =>
            {
                options.AddPolicy("Administration", policy => policy.RequireClaim("GlobalSettingsEdition"));
            });

            // Add gzip compression
            services.AddResponseCompression();

            // Add CORS
            if (appSettings.Cors.EnableCors)
            {
                CorsPolicyBuilder corsBuilder = new CorsPolicyBuilder();
                corsBuilder.AllowAnyHeader();
                corsBuilder.AllowAnyMethod();
                corsBuilder.AllowCredentials();

                if (appSettings.Cors.AllowAnyOrigin)
                {
                    corsBuilder.AllowAnyOrigin();
                }
                else if (!String.IsNullOrEmpty(appSettings.Cors.Origins))
                {
                    corsBuilder.WithOrigins(appSettings.Cors.Origins.Split(','));
                }

                services.AddCors(options =>
                {
                    options.AddPolicy("CorsPolicy", corsBuilder.Build());
                });
            }
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
        {
            AppSettings appSettings = Configuration.GetSection("AppSettings").Get<AppSettings>();

            // Add log4net logs
            loggerFactory.AddLog4Net("log4net.config");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Page/Error");
            }

            // Update database
            if (_isInstalled && appSettings.Configuration.EnableDatabaseUpdate)
            {
                UpdateDatabase(app);
            }

            // Routing
            app.UseRouting();

            // Cors
            if (appSettings.Cors.EnableCors)
            {
                app.UseCors("CorsPolicy");
            }

            if (appSettings.Configuration.DevelopmentMode)
            {
                CookiePolicyOptions cookiePolicyOptions = new CookiePolicyOptions
                {
                    CheckConsentNeeded = context => true,
                    Secure = CookieSecurePolicy.None,
                    MinimumSameSitePolicy = SameSiteMode.None
                };

                app.UseCookiePolicy(cookiePolicyOptions);
            }
            else
            {
                app.UseCookiePolicy();
            }

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseStatusCodePages();
            app.UseDefaultFiles();
            app.UseStaticFiles();

            if (_isInstalled)
            {
                IViewManager viewManager = serviceProvider.GetService<IViewManager>() as IViewManager;
                app.UseModuleStaticFiles(
                    viewManager,
                    appSettings.Configuration.ModuleDirectoryPath,
                    SiteConfiguration.DefaultModuleResourcesPath);
            }

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            // Count visits
            if (_isInstalled)
            {
                app.UseMiddleware<VisitorCounterMiddleware>();
            }

            // Add antiforgery validation
            if (!appSettings.Configuration.DevelopmentMode)
            {
                app.UseMiddleware<AntiForgeryTokenMiddleware>();
            }

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(name: "areaRoute",
                                pattern: "{area:exists}/{controller}/{action}/{id?}",
                                defaults: new { controller = "Page", action = "Home" });

                endpoints.MapControllerRoute(name: "adminRoute",
                                pattern: "{area:exists}/{*catchall}",
                                defaults: new { controller = "Home", action = "Index" });

                endpoints.AddDefaultEndpoints("Page", "Admin/Module", appSettings.Configuration.HasDefaultFallback);
            });
        }

        /// <summary>
        /// Updates the database.
        /// </summary>
        /// <param name="app">App.</param>
        private static void UpdateDatabase(IApplicationBuilder app)
        {
            using (IServiceScope serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                using (var context = serviceScope.ServiceProvider.GetService<ApplicationDbContext>())
                {
                    context.Database.Migrate();
                }
            }
        }

        /// <summary>
        /// Check if database has already been installed
        /// </summary>
        /// <param name="connectionString">Connection string</param>
        /// <returns></returns>
        private static Boolean HasTables(String connectionString)
        {
            int numberTables = 0;

            if (String.IsNullOrEmpty(connectionString))
                return false;

            string queryString = "SELECT Count(*) from sys.tables where name like 'Kastra%'";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(queryString, connection);
                numberTables = (int)command.ExecuteScalar();
            }

            return (numberTables > 0);
        }
    }
}
