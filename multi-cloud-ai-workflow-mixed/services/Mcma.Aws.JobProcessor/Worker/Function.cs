﻿using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Mcma.Aws.Client;
using Mcma.Aws.DynamoDb;
using Mcma.Aws.Lambda;
using Mcma.Aws.S3;
using Mcma.Client;
using Mcma.Core;
using Mcma.Core.Logging;
using Mcma.Core.Serialization;
using Mcma.Data;
using Mcma.Worker;
using Mcma.Worker.Builders;

[assembly: LambdaSerializer(typeof(McmaLambdaSerializer))]
[assembly: McmaLambdaLogger]

namespace Mcma.Aws.JobProcessor.Worker
{
    public class Function
    {
        static Function() => McmaTypes.Add<S3Locator>();
        private static IAuthProvider AuthProvider { get; } = new AuthProvider().AddAwsV4Auth(AwsV4AuthContext.Global);

        private static IResourceManagerProvider ResourceManagerProvider { get; } = new ResourceManagerProvider(AuthProvider);

        private static IDbTableProvider DbTableProvider { get; } = new DynamoDbTableProvider();

        private static IWorker Worker =
            new WorkerBuilder()
                .HandleOperation(new CreateJobAssignment(ResourceManagerProvider, AuthProvider, DbTableProvider))
                .HandleOperation(new DeleteJobAssignment(ResourceManagerProvider))
                .HandleOperation(new ProcessNotification(ResourceManagerProvider, DbTableProvider))
                .Build();

        public async Task Handler(WorkerRequest @event, ILambdaContext context)
        {
            Logger.Debug(@event.ToMcmaJson().ToString());
            Logger.Debug(context.ToMcmaJson().ToString());

            await Worker.DoWorkAsync(@event);
        }
    }
}
