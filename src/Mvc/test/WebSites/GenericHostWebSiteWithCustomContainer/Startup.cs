// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace GenericHostWebSiteWithCustomContainer
{
    public class Startup
    {
        // Set up application services
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers()
                .SetCompatibilityVersion(CompatibilityVersion.Latest);
            services.AddSingleton(new TestGenericService { Message = "ConfigureServices" });
        }

        public void ConfigureContainer(ThirdPartyContainer container) =>
                container.Services.AddSingleton(new TestGenericService { Message = "ConfigureContainer" });

        public void Configure(IApplicationBuilder app)
        {
            app.Use((ctx, next) => ctx.Response.WriteAsync(string.Join(',', ctx.RequestServices.GetServices<TestGenericService>().Select(x => x.Message))));
        }
    }
}
