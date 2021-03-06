﻿using System;
using System.Threading.Tasks;
using Mcma.Api.Routes;
using Mcma.Api.Routes.Defaults;
using Mcma.Azure.CosmosDb;
using Mcma.Azure.Functions.Api;
using Mcma.Azure.Functions.Logging;
using Mcma.Core;
using Mcma.Core.Context;
using Mcma.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

using McmaLogger = Mcma.Core.Logging.Logger;

namespace Mcma.Azure.ServiceRegistry.ApiHandler
{
    public static class Function
    {
        private static MicrosoftLoggerProvider LoggerProvider { get; } = new MicrosoftLoggerProvider("service-registry-api-handler");

        private static IDbTableProvider DbTableProvider { get; } =
            new CosmosDbTableProvider(new CosmosDbTableProviderOptions().FromEnvironmentVariables());

        private static McmaApiRouteCollection ServiceRoutes { get; } =
            new DefaultRouteCollectionBuilder<Service>(DbTableProvider).AddAll().WithSimpleQueryFiltering().Build();

        private static McmaApiRouteCollection JobProfileRoutes { get; } =
            new DefaultRouteCollectionBuilder<JobProfile>(DbTableProvider).AddAll().WithSimpleQueryFiltering().Build();

        private static AzureFunctionApiController ApiController { get; } =
            new McmaApiRouteCollection()
                .AddRoutes(ServiceRoutes)
                .AddRoutes(JobProfileRoutes)
                .ToAzureFunctionApiController(LoggerProvider);

        [FunctionName("ServiceRegistryApiHandler")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, Route = "{*resourcePath}")] HttpRequest request,
            string resourcePath,
            ILogger log,
            ExecutionContext executionContext)
        {
            return await ApiController.HandleRequestAsync(request, log);
        }
    }
}
