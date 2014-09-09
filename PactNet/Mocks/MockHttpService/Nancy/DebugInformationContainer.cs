using System.Collections.Generic;
using Nancy;

namespace PactNet.Mocks.MockHttpService.Nancy
{
    public class DebugInformationContainer : IDebugInformationContainer
    {
        private readonly List<object> _storage = new List<object>();

        public IEnumerable<object> All()
        {
            return _storage;
        }

        public void Record(Request request)
        {
            _storage.Add(new
            {
                request.Method,
                request.Path,
                request.Query
            });
        }
    }

    public interface IDebugInformationContainer
    {
        IEnumerable<object> All();
    }
}