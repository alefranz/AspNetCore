// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.DependencyInjection;

namespace GenericHostWebSiteWithCustomContainer
{
    public class ThirdPartyContainerServiceProviderFactory : IServiceProviderFactory<ThirdPartyContainer>
    {
        public ThirdPartyContainer CreateBuilder(IServiceCollection services) => new ThirdPartyContainer { Services = services };

        public IServiceProvider CreateServiceProvider(ThirdPartyContainer containerBuilder) => containerBuilder.Services.BuildServiceProvider();
    }
}
