using System.ClientModel;
using System.ClientModel.Primitives;

namespace IdvEnrichment.Functions.Tests;

/// <summary>
/// Builds a <see cref="ClientResultException"/> with a controllable status and headers. Every public
/// constructor on the real type requires a <see cref="PipelineResponse"/>, which is itself abstract, so a
/// minimal fake response/headers pair is the only way to construct one for tests.
/// </summary>
internal static class FakeClientResultException
{
    public static ClientResultException Create(int status, IReadOnlyDictionary<string, string>? headers = null) =>
        new(new FakeResponse(status, headers ?? new Dictionary<string, string>()), new Exception("Simulated failure"));

    private sealed class FakeResponse(int status, IReadOnlyDictionary<string, string> headers) : PipelineResponse
    {
        private readonly FakeHeaders headersInstance = new(headers);

        public override int Status { get; } = status;

        public override string ReasonPhrase => string.Empty;

        public override Stream? ContentStream { get; set; }

        public override BinaryData Content => BinaryData.Empty;

        protected override PipelineResponseHeaders HeadersCore => headersInstance;

        public override BinaryData BufferContent(CancellationToken cancellationToken = default) => BinaryData.Empty;

        public override ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default) =>
            new(BinaryData.Empty);

        public override void Dispose()
        {
        }
    }

    private sealed class FakeHeaders(IReadOnlyDictionary<string, string> values) : PipelineResponseHeaders
    {
        public override bool TryGetValue(string name, out string? value) => values.TryGetValue(name, out value);

        public override bool TryGetValues(string name, out IEnumerable<string>? values2)
        {
            values2 = null;
            return false;
        }

        public override IEnumerator<KeyValuePair<string, string>> GetEnumerator() => values.GetEnumerator();
    }
}
