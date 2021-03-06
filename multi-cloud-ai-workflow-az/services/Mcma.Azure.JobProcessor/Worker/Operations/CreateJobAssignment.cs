using System;
using System.Threading.Tasks;
using Mcma.Client;
using Mcma.Core;
using Mcma.Core.Context;
using Mcma.Core.Logging;
using Mcma.Data;
using Mcma.Worker;

namespace Mcma.Azure.JobProcessor.Worker
{
    internal class CreateJobAssignment : WorkerOperation<CreateJobAssignmentRequest>
    {
        public CreateJobAssignment(ProviderCollection providerCollection)
            : base(providerCollection)
        {
        }

        public override string Name => nameof(CreateJobAssignment);

        protected override async Task ExecuteAsync(WorkerRequest request, CreateJobAssignmentRequest createRequest)
        {
            var logger = ProviderCollection.LoggerProvider.Get(request.Tracker);

            var resourceManager = ProviderCollection.ResourceManagerProvider.Get(request);

            var table = ProviderCollection.DbTableProvider.Table<JobProcess>(request.TableName());

            var jobProcessId = createRequest.JobProcessId;
            var jobProcess = await table.GetAsync(jobProcessId);

            try
            {
                // retrieving the job
                var job = await resourceManager.GetAsync<Job>(jobProcess.Job);

                // retrieving the jobProfile
                var jobProfile = await resourceManager.GetAsync<JobProfile>(job.JobProfile);
                
                // validating job.JobInput with required input parameters of jobProfile
                var jobInput = job.JobInput; //await resourceManager.ResolveAsync<JobParameterBag>(job.JobInput);
                if (jobInput == null)
                    throw new Exception("Job is missing jobInput");

                if (jobProfile.InputParameters != null)
                {
                    foreach (var parameter in jobProfile.InputParameters)
                        if (!jobInput.HasProperty(parameter.ParameterName))
                            throw new Exception("jobInput is missing required input parameter '" + parameter.ParameterName + "'");
                }

                // finding a service that is capable of handling the job type and job profile
                var services = await resourceManager.QueryAsync<Service>();

                Service selectedService = null;
                ResourceEndpointClient jobAssignmentResourceEndpoint = null;
                
                foreach (var service in services)
                {
                    var serviceClient = resourceManager.GetServiceClient(service);

                    jobAssignmentResourceEndpoint = null;

                    if (service.JobType == job.Type)
                    {
                        jobAssignmentResourceEndpoint = serviceClient.GetResourceEndpointClient<JobAssignment>();
                        
                        if (jobAssignmentResourceEndpoint == null)
                            continue;

                        if (service.JobProfiles != null)
                        {
                            foreach (var serviceJobProfile in service.JobProfiles)
                            {
                                if (serviceJobProfile == job.JobProfile)
                                {
                                    selectedService = service;
                                    break;
                                }
                            }
                        }
                    }

                    if (selectedService != null)
                        break;
                }

                if (jobAssignmentResourceEndpoint == null)
                    throw new Exception("Failed to find service that could execute the " + job.GetType().Name);

                var jobAssignment = new JobAssignment
                {
                    Job = jobProcess.Job,
                    NotificationEndpoint = new NotificationEndpoint
                    {
                        HttpEndpoint = jobProcessId + "/notifications"
                    }
                };

                jobAssignment = await jobAssignmentResourceEndpoint.PostAsync<JobAssignment>(jobAssignment);

                jobProcess.Status = "SCHEDULED";
                jobProcess.JobAssignment = jobAssignment.Id;
            }
            catch (Exception error)
            {
                logger.Error("Failed to create job assignment", error);

                jobProcess.Status = JobStatus.Failed;
                jobProcess.StatusMessage = error.ToString();
            }

            jobProcess.DateModified = DateTime.UtcNow;

            await table.PutAsync(jobProcessId, jobProcess);

            await resourceManager.SendNotificationAsync(jobProcess, jobProcess.NotificationEndpoint);
        }
    }
}
