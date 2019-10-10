// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BasicWebSite
{
    public class StartupWithCustomContainer
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Latest);

            services.AddSingleton<ContactsRepository>();

            services.AddSingleton(new TestService { Message = "ConfigureServices" });
        }

        public void ConfigureContainer(ThirdPartyContainer container) =>
                container.Services.AddSingleton(new TestService { Message = "ConfigureContainer" });

        public void Configure(IApplicationBuilder app)
        {
            app.Use((ctx, next) => ctx.Response.WriteAsync(string.Join(',', ctx.RequestServices.GetServices<TestService>().Select(x => x.Message))));
        }

        public class ThirdPartyContainer
        {
            public IServiceCollection Services { get; set; }
        }

        public class ThirdPartyContainerServiceProviderFactory : IServiceProviderFactory<ThirdPartyContainer>
        {
            public ThirdPartyContainer CreateBuilder(IServiceCollection services) => new ThirdPartyContainer { Services = services };

            public IServiceProvider CreateServiceProvider(ThirdPartyContainer containerBuilder) => containerBuilder.Services.BuildServiceProvider();
        }
    }
}
