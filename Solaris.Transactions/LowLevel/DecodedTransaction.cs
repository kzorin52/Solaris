using System.Runtime.InteropServices;
using Solaris.Base.Account;
using Solaris.Borsh;

namespace Solaris.Transactions.LowLevel;

/// <summary>A validated, allocation-free view of a signed legacy or version 0 transaction.</summary>
/// <remarks>The input memory is borrowed, not copied. Retain it and do not mutate it while any decoded view is in use.
/// Parsing validates the wire format, not signatures or address table contents.</remarks>
public readonly struct DecodedTransaction
{
    private readonly int _signatureOffset;

    private DecodedTransaction(ReadOnlyMemory<byte> data, int signatureOffset, int signatureCount, DecodedMessage message)
    {
        Data = data;
        _signatureOffset = signatureOffset;
        SignatureCount = signatureCount;
        Message = message;
    }

    public ReadOnlyMemory<byte> Data { get; }
    public int SignatureCount { get; }
    public DecodedMessage Message { get; }

    public static DecodedTransaction Parse(ReadOnlyMemory<byte> data)
    {
        if (data.Length > 1232) throw new FormatException("Transaction exceeds the 1232-byte packet limit.");
        var reader = new BorshDeserializer(data.Span);
        var signatureCount = reader.CompactU16();
        var signatureOffset = reader.Offset;
        reader.Skip(signatureCount * 64);
        var message = DecodedMessage.Parse(data[reader.Offset..]);
        if (signatureCount != message.Header.RequiredSignatures)
            throw new FormatException("Signature count does not match the message header.");
        return new DecodedTransaction(data, signatureOffset, signatureCount, message);
    }

    public ReadOnlySpan<byte> GetSignature(int index)
    {
        if ((uint)index >= (uint)SignatureCount) throw new ArgumentOutOfRangeException(nameof(index));
        return Data.Span.Slice(_signatureOffset + index * 64, 64);
    }
}

/// <summary>A validated, allocation-free view of a serialized legacy or version 0 message.</summary>
/// <remarks>All slices borrow the input memory. Retain it and do not mutate it while this view or its children are in use.
/// Address table contents and signatures are not resolved or verified.</remarks>
public readonly struct DecodedMessage
{
    private readonly int _accountsOffset;
    private readonly int _accountCount;
    private readonly int _blockHashOffset;

    private DecodedMessage(ReadOnlyMemory<byte> data, byte? version, CompiledMessageHeader header,
        int accountsOffset, int accountCount, int blockHashOffset,
        DecodedInstructionCollection instructions, DecodedAddressTableLookupCollection addressTableLookups)
    {
        Data = data;
        Version = version;
        Header = header;
        _accountsOffset = accountsOffset;
        _accountCount = accountCount;
        _blockHashOffset = blockHashOffset;
        Instructions = instructions;
        AddressTableLookups = addressTableLookups;
    }

    public ReadOnlyMemory<byte> Data { get; }
    public byte? Version { get; }
    public CompiledMessageHeader Header { get; }
    public ReadOnlySpan<PublicKeyValue> Accounts => MemoryMarshal.Cast<byte, PublicKeyValue>(Data.Span.Slice(_accountsOffset, _accountCount * 32));
    public PublicKeyValue RecentBlockHash => PublicKeyValue.Create(Data.Span.Slice(_blockHashOffset, 32));
    public DecodedInstructionCollection Instructions { get; }
    public DecodedAddressTableLookupCollection AddressTableLookups { get; }

    public static DecodedMessage Parse(ReadOnlyMemory<byte> data)
    {
        var reader = new BorshDeserializer(data.Span);
        var first = reader.Byte();
        byte? version = null;
        if ((first & 0x80) != 0)
        {
            version = (byte)(first & 0x7f);
            if (version != 0) throw new NotSupportedException($"Transaction version {version} is not supported.");
            first = reader.Byte();
        }
        var header = new CompiledMessageHeader
        {
            RequiredSignatures = first,
            ReadOnlySignedAccounts = reader.Byte(),
            ReadOnlyUnsignedAccounts = reader.Byte()
        };
        var accountCount = reader.CompactU16();
        if (accountCount > 256 || header.RequiredSignatures > accountCount ||
            header.ReadOnlySignedAccounts >= header.RequiredSignatures ||
            header.ReadOnlyUnsignedAccounts > accountCount - header.RequiredSignatures)
            throw new FormatException("Invalid message header or account count.");
        var accountsOffset = reader.Offset;
        reader.Skip(accountCount * 32);
        var blockHashOffset = reader.Offset;
        reader.Skip(32);
        var instructionCount = reader.CompactU16();
        var instructionsOffset = reader.Offset;
        var maximumAccountIndex = -1;
        for (var i = 0; i < instructionCount; i++)
        {
            var programIdIndex = reader.Byte();
            if (programIdIndex >= accountCount)
                throw new FormatException("Instruction program index must reference a static account.");
            var keyCount = reader.CompactU16();
            var keyIndices = reader.Span(keyCount);
            foreach (var index in keyIndices)
                if (index > maximumAccountIndex) maximumAccountIndex = index;
            reader.Skip(reader.CompactU16());
        }
        var instructionsLength = reader.Offset - instructionsOffset;
        var lookupCount = 0;
        var lookupsOffset = reader.Offset;
        var totalAccountCount = accountCount;
        if (version.HasValue)
        {
            lookupCount = reader.CompactU16();
            lookupsOffset = reader.Offset;
            for (var i = 0; i < lookupCount; i++)
            {
                reader.Skip(32);
                var writableCount = reader.CompactU16();
                reader.Skip(writableCount);
                var readOnlyCount = reader.CompactU16();
                reader.Skip(readOnlyCount);
                totalAccountCount += writableCount + readOnlyCount;
                if (totalAccountCount > 256)
                    throw new FormatException("Static and loaded accounts exceed the byte-indexed account space.");
            }
        }
        if (maximumAccountIndex >= totalAccountCount)
            throw new FormatException("Instruction account index is out of range.");
        if (reader.Offset != data.Length) throw new FormatException("Unexpected trailing message data.");
        return new DecodedMessage(data, version, header, accountsOffset, accountCount, blockHashOffset,
            instructionCount == 0 ? default : new DecodedInstructionCollection(data.Slice(instructionsOffset, instructionsLength), instructionCount),
            lookupCount == 0 ? default : new DecodedAddressTableLookupCollection(data[lookupsOffset..], lookupCount));
    }
}

/// <summary>An instruction whose key indices and data borrow the parsed message memory.</summary>
public readonly struct DecodedInstruction
{
    internal DecodedInstruction(byte programIdIndex, ReadOnlyMemory<byte> keyIndices, ReadOnlyMemory<byte> data)
    {
        ProgramIdIndex = programIdIndex;
        KeyIndices = keyIndices;
        Data = data;
    }

    public byte ProgramIdIndex { get; }
    public ReadOnlyMemory<byte> KeyIndices { get; }
    public ReadOnlyMemory<byte> Data { get; }
}

/// <summary>An allocation-free, forward-only enumerable of borrowed instruction views.</summary>
public readonly struct DecodedInstructionCollection
{
    private readonly ReadOnlyMemory<byte> _data;

    internal DecodedInstructionCollection(ReadOnlyMemory<byte> data, int count)
    {
        _data = data;
        Count = count;
    }

    public int Count { get; }
    public Enumerator GetEnumerator() => new(_data, Count);

    public struct Enumerator
    {
        private readonly ReadOnlyMemory<byte> _data;
        private int _offset;
        private int _remaining;

        internal Enumerator(ReadOnlyMemory<byte> data, int count)
        {
            _data = data;
            _offset = 0;
            _remaining = count;
            Current = default;
        }

        public DecodedInstruction Current { get; private set; }

        public bool MoveNext()
        {
            if (_remaining == 0) return false;
            var reader = new BorshDeserializer(_data.Span) { Offset = _offset };
            var programIdIndex = reader.Byte();
            var keyCount = reader.CompactU16();
            var keyOffset = reader.Offset;
            reader.Skip(keyCount);
            var dataCount = reader.CompactU16();
            var dataOffset = reader.Offset;
            reader.Skip(dataCount);
            Current = new DecodedInstruction(programIdIndex,
                keyCount == 0 ? default : _data.Slice(keyOffset, keyCount),
                dataCount == 0 ? default : _data.Slice(dataOffset, dataCount));
            _offset = reader.Offset;
            _remaining--;
            return true;
        }
    }
}

/// <summary>An address lookup whose index slices borrow the parsed message memory.</summary>
public readonly struct DecodedAddressTableLookup
{
    internal DecodedAddressTableLookup(PublicKeyValue accountKey, ReadOnlyMemory<byte> writableIndices, ReadOnlyMemory<byte> readOnlyIndices)
    {
        AccountKey = accountKey;
        WritableIndices = writableIndices;
        ReadOnlyIndices = readOnlyIndices;
    }

    public PublicKeyValue AccountKey { get; }
    public ReadOnlyMemory<byte> WritableIndices { get; }
    public ReadOnlyMemory<byte> ReadOnlyIndices { get; }
}

/// <summary>An allocation-free, forward-only enumerable of borrowed address lookup views.</summary>
public readonly struct DecodedAddressTableLookupCollection
{
    private readonly ReadOnlyMemory<byte> _data;

    internal DecodedAddressTableLookupCollection(ReadOnlyMemory<byte> data, int count)
    {
        _data = data;
        Count = count;
    }

    public int Count { get; }
    public Enumerator GetEnumerator() => new(_data, Count);

    public struct Enumerator
    {
        private readonly ReadOnlyMemory<byte> _data;
        private int _offset;
        private int _remaining;

        internal Enumerator(ReadOnlyMemory<byte> data, int count)
        {
            _data = data;
            _offset = 0;
            _remaining = count;
            Current = default;
        }

        public DecodedAddressTableLookup Current { get; private set; }

        public bool MoveNext()
        {
            if (_remaining == 0) return false;
            var reader = new BorshDeserializer(_data.Span) { Offset = _offset };
            var accountKey = PublicKeyValue.Create(reader.Span(32));
            var writableCount = reader.CompactU16();
            var writableOffset = reader.Offset;
            reader.Skip(writableCount);
            var readOnlyCount = reader.CompactU16();
            var readOnlyOffset = reader.Offset;
            reader.Skip(readOnlyCount);
            Current = new DecodedAddressTableLookup(accountKey,
                writableCount == 0 ? default : _data.Slice(writableOffset, writableCount),
                readOnlyCount == 0 ? default : _data.Slice(readOnlyOffset, readOnlyCount));
            _offset = reader.Offset;
            _remaining--;
            return true;
        }
    }
}

