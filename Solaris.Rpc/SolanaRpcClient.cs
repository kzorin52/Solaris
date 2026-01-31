using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Solaris.Programs.Token;
using Solaris.Rpc.RpcModels;

namespace Solaris.Rpc;

public class SolanaRpcClient(string rpcUri) : IDisposable
{
    private const bool ThrowException = true;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _cli = CreateClient();

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

    private async Task<TResponse> MakeRawRequestAsync<TResponse>(object req)
    {
        using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, req, JsonOptions);
        stream.Seek(0, SeekOrigin.Begin);

        var content = new StreamContent(stream);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, rpcUri);
        request.Content = content;

        using var postResp = await _cli.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        
        await using var contentStream = await postResp.EnsureSuccessStatusCode().Content.ReadAsStreamAsync();

        var json = await JsonSerializer.DeserializeAsync<TResponse>(contentStream);
        return json!;
    }

    private async Task<TResponse?> QueryJsonRpcAsync<TResponse>(string method, object @params)
    {
        var jsonRpcResp = await MakeRawRequestAsync<BaseRpcResponse<TResponse>>(new
        {
            jsonrpc = "2.0",
            method,
            @params,
            id = Random.Shared.Next()
        });

        if (jsonRpcResp.Error != null && ThrowException)
            throw new SolanaRpcException(jsonRpcResp.Error.Code, jsonRpcResp.Error.Message);

        return jsonRpcResp.Result;
    }

    private async Task<TResponse?> QueryJsonRpcUnwrapContextAsync<TResponse>(string method, object @params)
    {
        return (await QueryJsonRpcAsync<ContextWrapped<TResponse>>(method, @params))!.Value;
    }

    public async Task<GPASingleResult[]> GetProgramAccounts(string programId, GPAFilters? filters = null)
    {
        return (await QueryJsonRpcAsync<GPASingleResult[]>("getProgramAccounts", (object?[])
        [
            programId,
            filters ?? GPAFilters.Empty
        ]))!;
    }

    public async Task<SolanaAccount?> GetAccountInfo(string account, string commitment = "processed",
        string encoding = "base64", DataSlice? dataSlice = null)
    {
        return (await QueryJsonRpcUnwrapContextAsync<SolanaAccount>("getAccountInfo", (object?[])
        [
            account,
            new
            {
                commitment,
                encoding,
                dataSlice
            }
        ]))!;
    }

    public async Task<LatestBlockhash> GetLatestBlockhash(string commitment = "processed")
    {
        return (await QueryJsonRpcUnwrapContextAsync<LatestBlockhash>("getLatestBlockhash", (object?[])
        [
            new
            {
                commitment
            }
        ]))!;
    }

    public async Task<string> SendTransaction(string transaction, string encoding = "base64", int? maxRetries = null)
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
        ]))!;
    }

    public async Task<SimulateTransactionResult> SimulateTransaction(string transaction,
        SimulateTransactionConfig? config = null)
    {
        return (await QueryJsonRpcUnwrapContextAsync<SimulateTransactionResult>("simulateTransaction", (object?[])
        [
            transaction,
            config ?? new SimulateTransactionConfig()
        ]))!;
    }

    public async Task<TokenAccount[]> GetTokenAccountsByOwner(string owner, string programId, string? mint = null,
        string commitment = "processed")
    {
        var gtaResp = (await QueryJsonRpcUnwrapContextAsync<GPASingleResult[]>("getTokenAccountsByOwner", (object?[])
        [
            owner,
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
        ]))!;

        var result = new TokenAccount[gtaResp.Length];
        for (var i = 0; i < gtaResp.Length; i++) result[i] = new TokenAccount(gtaResp[i].DataSpan);

        return result;
    }
}

public class SolanaRpcException(int errorCode, string message)
    : Exception($"Error code: {errorCode}, Message: {message}");