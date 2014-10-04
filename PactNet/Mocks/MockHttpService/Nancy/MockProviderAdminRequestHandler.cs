using System;
using System.IO;
using System.Linq;
using System.Text;
using Nancy;
using Newtonsoft.Json;
using PactNet.Configuration.Json;
using PactNet.Mocks.MockHttpService.Comparers;
using PactNet.Reporters;

namespace PactNet.Mocks.MockHttpService.Nancy
{
    public class MockProviderAdminRequestHandler : IMockProviderAdminRequestHandler
    {
        private readonly IMockProviderRepository _mockProviderRepository;
        private readonly IProviderServiceRequestComparer _requestComparer;
        private readonly IReporter _reporter;
        private readonly IDebugInformationContainer _debugInformationContainer;

        public MockProviderAdminRequestHandler(
            IMockProviderRepository mockProviderRepository,
            IReporter reporter,
            IProviderServiceRequestComparer requestComparer,
            IDebugInformationContainer debugInformationContainer)
        {
            _mockProviderRepository = mockProviderRepository;
            _reporter = reporter;
            _requestComparer = requestComparer;
            _debugInformationContainer = debugInformationContainer;
        }

        public Response Handle(NancyContext context)
        {
            if (context.Request.Method.Equals("DELETE", StringComparison.InvariantCultureIgnoreCase) &&
                context.Request.Path == "/interactions")
            {
                return HandleDeleteInteractions();
            }

            if (context.Request.Method.Equals("GET", StringComparison.InvariantCultureIgnoreCase) &&
                context.Request.Path == "/debug")
            {
                return HandleDebugInformationRequest();
            }
            
            if (context.Request.Method.Equals("GET", StringComparison.InvariantCultureIgnoreCase) &&
                context.Request.Path == "/interactions/verification")
            {
                return HandleGetInteractionsVerificationRequest(context);
            }

            return GenerateResponse(HttpStatusCode.NotFound,
                String.Format("The {0} request for path {1}, does not have a matching mock provider admin action.", context.Request.Method, context.Request.Path));
        }

        private Response HandleDebugInformationRequest()
        {
            var debugEntries = _debugInformationContainer.All();
            var json = JsonConvert.SerializeObject(debugEntries, JsonConfig.ApiSerializerSettings);
            return GenerateResponse(HttpStatusCode.OK, json);
        }

        private Response HandleDeleteInteractions()
        {
            _mockProviderRepository.ClearHandledRequests();
            return GenerateResponse(HttpStatusCode.OK, "Successfully cleared the handled requests.");
        }

        private Response HandleGetInteractionsVerificationRequest(NancyContext context)
        {
            //Check all registered interactions have been used once and only once
            var registeredInteractions = context.GetMockInteractions().ToList();
            if (registeredInteractions.Any())
            {
                foreach (var registeredInteraction in registeredInteractions)
                {
                    var interactionUsages = _mockProviderRepository.HandledRequests.Where(x => x.MatchedInteraction == registeredInteraction).ToList();

                    if (interactionUsages == null || !interactionUsages.Any())
                    {
                        _reporter.ReportError(String.Format("Registered mock interaction with description '{0}' and provider state '{1}', was not used by the test.", registeredInteraction.Description, registeredInteraction.ProviderState));
                    }
                    else if (interactionUsages.Count() > 1)
                    {
                        _reporter.ReportError(String.Format("Registered mock interaction with description '{0}' and provider state '{1}', was used {2} time/s by the test.", registeredInteraction.Description, registeredInteraction.ProviderState, interactionUsages.Count()));
                    }
                }
            }
            else
            {
                if (_mockProviderRepository.HandledRequests != null && _mockProviderRepository.HandledRequests.Any())
                {
                    _reporter.ReportError("No mock interactions were registered, however the mock provider service was called.");
                }
            }

            //Check all handled requests actually match the registered interaction
            if (_mockProviderRepository.HandledRequests != null &&
                _mockProviderRepository.HandledRequests.Any())
            {
                foreach (var handledRequest in _mockProviderRepository.HandledRequests)
                {
                    _requestComparer.Compare(handledRequest.MatchedInteraction.Request, handledRequest.ActualRequest);
                }
            }

            try
            {
                _reporter.ThrowIfAnyErrors();
            }
            catch (Exception ex)
            {
                _reporter.ClearErrors();
                return GenerateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }

            return GenerateResponse(HttpStatusCode.OK, "Successfully verified mock provider interactions.");
        }

        private Response GenerateResponse(HttpStatusCode statusCode, string message)
        {
            return new Response
            {
                StatusCode = statusCode,
                Contents = s => SetContent(message, s)
            };
        }

        private void SetContent(string content, Stream stream)
        {
            var contentBytes = Encoding.UTF8.GetBytes(content);
            stream.Write(contentBytes, 0, contentBytes.Length);
            stream.Flush();
        }
    }
}