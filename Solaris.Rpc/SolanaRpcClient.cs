using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Programs.Token;
using Solaris.Rpc.RpcModels;

namespace Solaris.Rpc;

public class SolanaRpcClient(string rpcUri, HttpClient? client = null) : IDisposable
{
    private const bool ThrowException = true;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _cli = client ?? CreateClient();

    public void Dispose()
    {
        _cli.Dispose();
    }

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler();
        handler.AutomaticDecompression = DecompressionMethods.All;
        handler.MaxConnectionsPerServer = int.MaxValue;

        var cli = new HttpClient(handler, true);
        cli.Timeout = TimeSpan.FromSeconds(25);

        cli.DefaultRequestHeaders.Add("Accept", "application/json");
        cli.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        cli.DefaultRequestHeaders.TransferEncodingChunked = true;

        return cli;
    }

    private async Task<TResponse> MakeRawRequestAsync<TResponse>(object req, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, req, JsonOptions, cancellationToken);
        stream.Seek(0, SeekOrigin.Begin);

        var content = new StreamContent(stream);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, rpcUri);
        request.Content = content;

        using var postResp = await _cli.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        await using var contentStream = await postResp.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(cancellationToken);

        var json = await JsonSerializer.DeserializeAsync<TResponse>(contentStream, cancellationToken: cancellationToken);
        return json!;
    }

    private async Task<TResponse?> QueryJsonRpcAsync<TResponse>(string method, object @params,
        CancellationToken cancellationToken = default)
    {
        var jsonRpcResp = await MakeRawRequestAsync<BaseRpcResponse<TResponse>>(new
        {
            jsonrpc = "2.0",
            method,
            @params,
            id = Random.Shared.Next()
        }, cancellationToken);

        if (jsonRpcResp.Error != null && ThrowException)
            throw new SolanaRpcException(jsonRpcResp.Error.Code, jsonRpcResp.Error.Message);

        return jsonRpcResp.Result;
    }

    private async Task<TResponse?> QueryJsonRpcUnwrapContextAsync<TResponse>(string method, object @params,
        CancellationToken cancellationToken = default)
    {
        return (await QueryJsonRpcAsync<ContextWrapped<TResponse>>(method, @params, cancellationToken))!.Value;
    }

    public async Task<GPASingleResult[]> GetProgramAccounts(string programId, GPAFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        return (await QueryJsonRpcAsync<GPASingleResult[]>("getProgramAccounts", (object?[])
        [
            programId,
            filters ?? GPAFilters.Empty
        ], cancellationToken))!;
    }

    public async Task<SolanaAccount?> GetAccountInfo(PublicKey account, string commitment = "processed",
        string encoding = "base64", DataSlice? dataSlice = null, CancellationToken cancellationToken = default)
    {
        return (await QueryJsonRpcUnwrapContextAsync<SolanaAccount>("getAccountInfo", (object?[])
        [
            account.Key,
            new
            {
                commitment,
                encoding,
                dataSlice
            }
        ], cancellationToken))!;
    }
    
    public async Task<SolanaAccount?[]> GetMultipleAccounts(IEnumerable<PublicKey> accounts, string commitment = "processed",
        string encoding = "base64", DataSlice? dataSlice = null, ulong? minContextSlot = null,
        CancellationToken cancellationToken = default)
    {
        return (await GetMultipleAccountsWithContext(accounts, commitment, encoding, dataSlice, minContextSlot,
            cancellationToken)).Value!;
    }

    public async Task<ContextWrapped<SolanaAccount?[]>> GetMultipleAccountsWithContext(
        IEnumerable<PublicKey> accounts, string commitment = "processed", string encoding = "base64",
        DataSlice? dataSlice = null, ulong? minContextSlot = null, CancellationToken cancellationToken = default)
    {
        return (await QueryJsonRpcAsync<ContextWrapped<SolanaAccount?[]>>("getMultipleAccounts", (object?[])
        [
            accounts.Select(x => x.Key).ToArray(),
            new
            {
                commitment,
                encoding,
                dataSlice,
                minContextSlot
            }
        ], cancellationToken))!;
    }

    public async Task<LatestBlockhash> GetLatestBlockhash(string commitment = "processed",
        CancellationToken cancellationToken = default)
    {
        return (await QueryJsonRpcUnwrapContextAsync<LatestBlockhash>("getLatestBlockhash", (object?[])
        [
            new
            {
                commitment
            }
        ], cancellationToken))!;
    }

    public async Task<ulong> GetSlot(string commitment = "processed", ulong? minContextSlot = null,
        CancellationToken cancellationToken = default)
    {
        return await QueryJsonRpcAsync<ulong>("getSlot", (object?[])
        [
            new
            {
                commitment,
                minContextSlot
            }
        ], cancellationToken);
    }

    public async Task<ulong> GetBlockHeight(string commitment = "processed", ulong? minContextSlot = null,
        CancellationToken cancellationToken = default)
    {
        return await QueryJsonRpcAsync<ulong>("getBlockHeight", (object?[])
        [
            new
            {
                commitment,
                minContextSlot
            }
        ], cancellationToken);
    }

    public async Task<AddressSignature[]> GetSignaturesForAddress(PublicKey address, int limit = 100,
        string? before = null, string commitment = "confirmed", CancellationToken cancellationToken = default)
    {
        return (await QueryJsonRpcAsync<AddressSignature[]>("getSignaturesForAddress", (object?[])
        [
            address.Key,
            new { limit, before, commitment }
        ], cancellationToken))!;
    }

    public Task<HistoricalTransaction?> GetTransaction(string signature, string commitment = "confirmed",
        int? maxSupportedTransactionVersion = 0, CancellationToken cancellationToken = default)
    {
        return QueryJsonRpcAsync<HistoricalTransaction>("getTransaction", (object?[])
        [
            signature,
            new { commitment, encoding = "base64", maxSupportedTransactionVersion }
        ], cancellationToken);
    }

    public async Task<LargestTokenAccount[]> GetTokenLargestAccounts(PublicKey mint, string commitment = "confirmed",
        CancellationToken cancellationToken = default)
    {
        return (await QueryJsonRpcUnwrapContextAsync<LargestTokenAccount[]>("getTokenLargestAccounts", (object?[])
        [
            mint.Key,
            new { commitment }
        ], cancellationToken))!;
    }

    public async Task<ContextWrapped<SignatureStatus?[]>> GetSignatureStatuses(
        IReadOnlyList<string> signatures, SignatureStatusesConfig? config = null, CancellationToken cancellationToken = default)
    {
        return (await QueryJsonRpcAsync<ContextWrapped<SignatureStatus?[]>>("getSignatureStatuses",
            config is null ? (object?[])[signatures] : [signatures, config], cancellationToken))!;
    }

    // skipPreflight is very intentional since using preflight check instead of separate simulateTransaction is a very bad practice.
    public async Task<string> SendTransaction(string transaction, string encoding = "base64", int? maxRetries = null,
        CancellationToken cancellationToken = default)
    {
        return (await QueryJsonRpcAsync<string>("sendTransaction", (object?[])
        [
            transaction,
            new
            {
                encoding,
                skipPreflight = true,
                maxRetries
            }
        ], cancellationToken))!;
    }

    public async Task<SimulateTransactionResult> SimulateTransaction(string transaction,
        SimulateTransactionConfig? config = null, CancellationToken cancellationToken = default)
    {
        return (await QueryJsonRpcUnwrapContextAsync<SimulateTransactionResult>("simulateTransaction", (object?[])
        [
            transaction,
            config ?? new SimulateTransactionConfig()
        ], cancellationToken))!;
    }

    public async Task<TokenAccount[]> GetTokenAccountsByOwner(PublicKey owner, string programId, string? mint = null,
        string commitment = "processed", CancellationToken cancellationToken = default)
    {
        var gtaResp = (await QueryJsonRpcUnwrapContextAsync<GPASingleResult[]>("getTokenAccountsByOwner", (object?[])
        [
            owner.Key,
            new
            {
                mint,
                programId
            },
            new
            {
                commitment,
                encoding = "base64"
            }
        ], cancellationToken))!;

        var result = new TokenAccount[gtaResp.Length];
        for (var i = 0; i < gtaResp.Length; i++) result[i] = gtaResp[i].DataSpan.Deserialize<TokenAccount>(gtaResp[i].PubKey);

        return result;
    }
}

public class SolanaRpcException(int errorCode, string message)
    : Exception($"Error code: {errorCode}, Message: {message}");