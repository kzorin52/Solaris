using System.Text.Json;
using System.Text.Json.Serialization;
using Solaris.Transactions.LowLevel;

namespace Solaris.Rpc.RpcModels;

// Bind the RPC envelope directly so JSON reflection never traverses the span-based binary views.
internal sealed class HistoricalTransactionConverter : JsonConverter<HistoricalTransaction>
{
    public override HistoricalTransaction Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected a transaction result object.");

        ulong slot = 0;
        long? blockTime = null;
        TransactionVersion? version = null;
        HistoricalTransactionMeta? meta = null;
        DecodedTransaction transaction = default;
        var hasTransaction = false;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (!hasTransaction)
                    throw new JsonException("A transaction result requires a binary transaction.");
                return new HistoricalTransaction
                {
                    Slot = slot, BlockTime = blockTime, Version = version, Meta = meta, Transaction = transaction
                };
            }
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected a transaction result property.");
            var isSlot = reader.ValueTextEquals("slot"u8);
            var isBlockTime = reader.ValueTextEquals("blockTime"u8);
            var isVersion = reader.ValueTextEquals("version"u8);
            var isMeta = reader.ValueTextEquals("meta"u8);
            var isTransaction = reader.ValueTextEquals("transaction"u8);
            if (!reader.Read())
                throw new JsonException("Missing transaction result property value.");
            if (isSlot)
            {
                if (reader.TokenType != JsonTokenType.Number || !reader.TryGetUInt64(out slot))
                    throw new JsonException("Transaction slot must be an unsigned 64-bit integer.");
            }
            else if (isBlockTime)
            {
                if (reader.TokenType == JsonTokenType.Null)
                    blockTime = null;
                else if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var timestamp))
                    blockTime = timestamp;
                else
                    throw new JsonException("Transaction block time must be a signed 64-bit integer or null.");
            }
            else if (isVersion)
                version = JsonSerializer.Deserialize<TransactionVersion?>(ref reader, options);
            else if (isMeta)
                meta = JsonSerializer.Deserialize<HistoricalTransactionMeta>(ref reader, options);
            else if (isTransaction)
            {
                transaction = ReadTransaction(ref reader);
                hasTransaction = true;
            }
            else
                reader.Skip();
        }
        throw new JsonException("Unterminated transaction result.");
    }

    private static DecodedTransaction ReadTransaction(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray || !reader.Read() || reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected a [base64Data, \"base64\"] transaction tuple.");

        // Validate the complete tuple before allocating its single owned binary buffer.
        var end = reader;
        if (!end.Read() || end.TokenType != JsonTokenType.String || !end.ValueTextEquals("base64"u8))
            throw new JsonException("Transactions must use base64 encoding.");
        if (!end.Read() || end.TokenType != JsonTokenType.EndArray)
            throw new JsonException("Transaction tuples must contain exactly two strings.");

        DecodedTransaction transaction;
        try
        {
            transaction = DecodedTransaction.Parse(reader.GetBytesFromBase64());
        }
        catch (FormatException exception)
        {
            throw new JsonException("Invalid base64 transaction packet.", exception);
        }
        reader = end;
        return transaction;
    }

    public override void Write(Utf8JsonWriter writer, HistoricalTransaction value, JsonSerializerOptions options) =>
        throw new NotSupportedException("RPC responses are deserialization-only.");
}

internal sealed class TransactionVersionConverter : JsonConverter<TransactionVersion>
{
    public override TransactionVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.ValueTextEquals("legacy"u8))
            return TransactionVersion.Legacy;
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetByte(out var number))
            return new TransactionVersion(number);
        throw new JsonException("A transaction version must be 'legacy' or an unsigned byte.");
    }

    public override void Write(Utf8JsonWriter writer, TransactionVersion value, JsonSerializerOptions options) =>
        throw new NotSupportedException("RPC responses are deserialization-only.");
}
