﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using PactNet.Mocks.MockHttpService.Models;
using PactNet.Mocks.MockHttpService.Nancy;
using PactNet.Models;

namespace PactNet.Mocks.MockHttpService
{
    public class MockProviderService : IMockProviderService
    {
        private readonly Func<Uri, IMockContextService, IHttpHost> _hostFactory;
        private IHttpHost _host;
        private readonly Func<string, HttpClient> _httpClientFactory; 

        private string _providerState;
        private string _description;
        private ProviderServiceRequest _request;
        private ProviderServiceResponse _response;

        private readonly IList<ProviderServiceInteraction> _testScopedInteractions = new List<ProviderServiceInteraction>();
        public IEnumerable<Interaction> TestScopedInteractions
        {
            get { return _testScopedInteractions; }
        }

        private readonly IList<ProviderServiceInteraction> _interactions = new List<ProviderServiceInteraction>();
        public IEnumerable<Interaction> Interactions
        {
            get { return _interactions; }
        }

        public string BaseUri { get; private set; }

        internal MockProviderService(
            Func<Uri, IMockContextService, IHttpHost> hostFactory,
            int port,
            bool enableSsl,
            Func<string, HttpClient> httpClientFactory)
        {
            _hostFactory = hostFactory;
            BaseUri = String.Format("{0}://localhost:{1}", enableSsl ? "https" : "http", port);
            _httpClientFactory = httpClientFactory;
        }

        public MockProviderService(int port, bool enableSsl)
            : this(
            (baseUri, mockContextService) => new NancyHttpHost(baseUri, mockContextService), 
            port,
            enableSsl,
            baseUri => new HttpClient { BaseAddress = new Uri(baseUri) })
        {
        }

        public IMockProviderService Given(string providerState)
        {
            if (String.IsNullOrEmpty(providerState))
            {
                throw new ArgumentException("Please supply a non null or empty providerState");
            }

            _providerState = providerState;

            return this;
        }

        public IMockProviderService UponReceiving(string description)
        {
            if (String.IsNullOrEmpty(description))
            {
                throw new ArgumentException("Please supply a non null or empty description");
            }

            _description = description;

            return this;
        }

        public IMockProviderService With(ProviderServiceRequest request)
        {
            if (request == null)
            {
                throw new ArgumentException("Please supply a non null request");
            }

            _request = request;
            
            return this;
        }

        public void WillRespondWith(ProviderServiceResponse response)
        {
            if (response == null)
            {
                throw new ArgumentException("Please supply a non null response");
            }

            _response = response;

            RegisterInteraction();
        }

        public void VerifyInteractions()
        {
            if (_host != null)
            {
                PerformAdminHttpRequest(HttpMethod.Get, "/interactions/verification");
            }
            else
            {
                throw new InvalidOperationException("Unable to verify interactions because the mock provider service is not running.");
            }
        }
        
        public void Debug()
        {
            if (_host != null)
            {
                PerformAdminHttpRequest(HttpMethod.Get, "/debug");
            }
            else
            {
                throw new InvalidOperationException("Unable to verify interactions because the mock provider service is not running.");
            }
        }

        private void RegisterInteraction()
        {
            if (String.IsNullOrEmpty(_description))
            {
                throw new InvalidOperationException("description has not been set, please supply using the UponReceiving method.");
            }

            if (_request == null)
            {
                throw new InvalidOperationException("request has not been set, please supply using the With method.");
            }

            if (_response == null)
            {
                throw new InvalidOperationException("response has not been set, please supply using the WillRespondWith method.");
            }

            var interaction = new ProviderServiceInteraction
            {
                ProviderState = _providerState,
                Description = _description,
                Request = _request,
                Response = _response
            };

            //You cannot have any duplicate interaction defined in a test scope
            if (_testScopedInteractions.Any(x => x.Description == interaction.Description &&
                x.ProviderState == interaction.ProviderState))
            {
                throw new InvalidOperationException(String.Format("An interaction already exists with the description '{0}' and provider state '{1}' in this test. Please supply a different description or provider state.", interaction.Description, interaction.ProviderState));
            }

            //From a Pact specification perspective, I should de-dupe any interactions that have been registered by another test as long as they match exactly!
            var duplicateInteractions = _interactions.Where(x => x.Description == interaction.Description && x.ProviderState == interaction.ProviderState).ToList();
            if (!duplicateInteractions.Any())
            {
                _interactions.Add(interaction);
            }
            else
            {
                //If the interaction description and provider state match, however anything else in the interaction is different, throw
                if (duplicateInteractions.Any(di => di.ToString() != interaction.ToString()))
                {
                    throw new InvalidOperationException(String.Format("An interaction registered by another test already exists with the description '{0}' and provider state '{1}', however the interaction does not match perfectly. Please supply a different description or provider state. Alternatively align this interaction to match the duplicate exactly.", interaction.Description, interaction.ProviderState));
                }
            }

            _testScopedInteractions.Add(interaction);

            ClearTrasientState();
        }

        public void Start()
        {
            StopRunningHost();
            _host = _hostFactory(new Uri(BaseUri), new MockContextService(GetMockInteractions));
            _host.Start();
        }

        public void Stop()
        {
            ClearAllState();
            StopRunningHost();
        }

        public void ClearInteractions()
        {
            _testScopedInteractions.Clear();

            if (_host != null)
            {
                PerformAdminHttpRequest(HttpMethod.Delete, "/interactions");
            }
        }

        private void StopRunningHost()
        {
            if (_host != null)
            {
                _host.Stop();
                _host = null;
            }
        }

        private void ClearAllState()
        {
            ClearTrasientState();
            ClearInteractions();
            _interactions.Clear();
        }

        private void ClearTrasientState()
        {
            _request = null;
            _response = null;
            _providerState = null;
            _description = null;
        }

        private IEnumerable<ProviderServiceInteraction> GetMockInteractions()
        {
            if (!_testScopedInteractions.Any())
            {
                return null;
            }

            return _testScopedInteractions;
        }

        private void PerformAdminHttpRequest(HttpMethod httpMethod, string path)
        {
            HttpStatusCode responseStatusCode;
            var responseContent = String.Empty;

            using (var client = _httpClientFactory(BaseUri))
            {
                var request = new HttpRequestMessage(httpMethod, path);
                request.Headers.Add(Constants.AdministrativeRequestHeaderKey, "true");
                var response = client.SendAsync(request, CancellationToken.None).Result;
                responseStatusCode = response.StatusCode;
                
                if (response.Content != null)
                {
                    responseContent = response.Content.ReadAsStringAsync().Result;
                }

                request.Dispose();
                response.Dispose();
            }

            if (responseStatusCode != HttpStatusCode.OK)
            {
                throw new PactFailureException(responseContent);
            }
        }
    }
}
