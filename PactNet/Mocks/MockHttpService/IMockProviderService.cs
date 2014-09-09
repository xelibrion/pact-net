using PactNet.Mocks.MockHttpService.Models;

namespace PactNet.Mocks.MockHttpService
{
    public interface IMockProviderService : IMockProvider<IMockProviderService>
    {
        IMockProviderService With(ProviderServiceRequest request);
        void WillRespondWith(ProviderServiceResponse response);
        void Start();
        void Stop();
        void ClearInteractions();
        void VerifyInteractions();
        void Debug();
    }
}